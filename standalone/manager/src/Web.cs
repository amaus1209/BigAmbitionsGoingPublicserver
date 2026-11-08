using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace GpsManager;

/// <summary>The web interface: the admin pages (embedded in the program, behind the login) and, without a login, the
/// players' ranking page under /monitor/ (server/telemetry-data/web - the same page and key folders as under AMP, so the
/// mod's mod list at &lt;PlayerWebUrl&gt;/monitor/&lt;key folder&gt;/mods/mods.json works too).
/// Every request that changes something needs the header X-GPS (a page of another site cannot send it) and the session
/// cookie (HttpOnly, SameSite=Strict).</summary>
internal static class Web
{
    private static readonly Dictionary<string, (byte[] Data, string Type)> Embedded = LoadEmbedded();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Dictionary<string, (byte[], string)> LoadEmbedded()
    {
        var map = new Dictionary<string, (byte[], string)>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        var types = new FileExtensionContentTypeProvider();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("www/", StringComparison.Ordinal)) continue;
            using var s = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            string path = "/" + name[4..].Replace('\\', '/');
            map[path] = (ms.ToArray(), types.TryGetContentType(path, out var t) ? t + (t.StartsWith("text/") || t.EndsWith("javascript") ? "; charset=utf-8" : "") : "application/octet-stream");
        }
        return map;
    }

    internal static void Map(WebApplication app)
    {
        // security headers; the admin pages may only load their own files
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "no-referrer";
            if (!ctx.Request.Path.StartsWithSegments("/monitor"))
            {
                h["X-Frame-Options"] = "DENY";
                h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            }
            await next();
        });

        // the admin page itself
        app.Use(async (ctx, next) =>
        {
            string path = ctx.Request.Path.Value ?? "/";
            if (path == "/") path = "/index.html";
            if (HttpMethods.IsGet(ctx.Request.Method) && Embedded.TryGetValue(path, out var f))
            {
                ctx.Response.ContentType = f.Type;
                ctx.Response.Headers.CacheControl = "no-cache";
                await ctx.Response.Body.WriteAsync(f.Data);
                return;
            }
            await next();
        });

        // the players' ranking page (no login - the key folders protect themselves); no folder listing
        Directory.CreateDirectory(Paths.Web);
        var monitor = new PhysicalFileProvider(Paths.Web, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.Sensitive);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = monitor,
            RequestPath = "/monitor",
            OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
        });
        app.MapGet("/monitor", () => Results.Redirect("/monitor/index.html"));   // routing takes "/monitor/" too

        // login gate for the API
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (!path.StartsWithSegments("/api")) { await next(); return; }
            if (!HttpMethods.IsGet(ctx.Request.Method) && ctx.Request.Headers["X-GPS"] != "1")
            {
                ctx.Response.StatusCode = 403;
                return;
            }
            if (path.StartsWithSegments("/api/login") || path.StartsWithSegments("/api/session")) { await next(); return; }
            string? user = Sessions.UserOf(ctx.Request.Cookies[Sessions.Cookie]);
            if (user == null)
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new { error = "login" });
                return;
            }
            ctx.Items["user"] = user;
            await next();
        });

        // ── session ──────────────────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/session", (HttpContext ctx) =>
            Results.Json(new { user = Sessions.UserOf(ctx.Request.Cookies[Sessions.Cookie]), lang = Lang.German ? "de" : "en", version = Program.Version }, Json));

        app.MapPost("/api/login", async (HttpContext ctx) =>
        {
            string ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
            var locked = Sessions.LockedFor(ip);
            if (locked > TimeSpan.Zero)
                return Results.Json(new { error = Lang.T($"Zu viele Fehlversuche - in {Math.Ceiling(locked.TotalMinutes)} min erneut versuchen.", $"Too many failed logins - try again in {Math.Ceiling(locked.TotalMinutes)} min.") }, Json, statusCode: 429);
            var body = await ReadBody(ctx);
            string user = (string?)body["user"] ?? "", pass = (string?)body["password"] ?? "";
            if (user.Length == 0 || pass.Length == 0 || !Users.Verify(user, pass))
            {
                Sessions.Failed(ip);
                await Task.Delay(600);
                Log.Warn(Lang.T($"Fehlgeschlagene Anmeldung für '{Clip(user)}' von {ip}.", $"Failed login for '{Clip(user)}' from {ip}."));
                return Results.Json(new { error = Lang.T("Benutzername oder Passwort falsch.", "Wrong user name or password.") }, Json, statusCode: 401);
            }
            Sessions.Succeeded(ip);
            string token = Sessions.Create(user);
            ctx.Response.Cookies.Append(Sessions.Cookie, token, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps, Path = "/", MaxAge = Sessions.Lifetime, IsEssential = true,
            });
            Log.Info(Lang.T($"Anmeldung: {user} von {ip}.", $"Login: {user} from {ip}."));
            return Results.Json(new { user }, Json);
        });

        app.MapPost("/api/logout", (HttpContext ctx) =>
        {
            Sessions.End(ctx.Request.Cookies[Sessions.Cookie]);
            ctx.Response.Cookies.Delete(Sessions.Cookie, new CookieOptions { Path = "/" });
            return Results.Json(new { ok = true }, Json);
        });

        app.MapPost("/api/account/password", async (HttpContext ctx) =>
        {
            var body = await ReadBody(ctx);
            string err = Users.ChangePassword((string)ctx.Items["user"]!, (string?)body["current"] ?? "", (string?)body["next"] ?? "");
            if (err.Length > 0) return Results.Json(new { error = err }, Json, statusCode: 400);
            Log.Info(Lang.T($"Passwort von {ctx.Items["user"]} geändert.", $"Password of {ctx.Items["user"]} changed."));
            return Results.Json(new { ok = true }, Json);
        });

        // ── status and control ───────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/status", () => Results.Content(Status().ToJsonString(), "application/json; charset=utf-8"));

        app.MapPost("/api/server/{action}", (string action, HttpContext ctx) =>
        {
            string user = (string)ctx.Items["user"]!;
            string err = action switch
            {
                "start" => GameServer.Start(Lang.T($"von {user}", $"by {user}")),
                "stop" => GameServer.Stop(Lang.T($"von {user}", $"by {user}")),
                "restart" => GameServer.Stop(Lang.T($"Neustart von {user}", $"restart by {user}"), restart: true),
                "kill" => Do(() => GameServer.Kill(Lang.T($"von {user}", $"by {user}"))),
                _ => "?",
            };
            return err.Length == 0 ? Results.Json(new { ok = true }, Json) : Results.Json(new { error = err }, Json, statusCode: 409);
        });

        app.MapPost("/api/server/command", async (HttpContext ctx) =>
        {
            var body = await ReadBody(ctx);
            string line = ((string?)body["line"] ?? "").Trim();
            if (line.Length == 0) return Results.Json(new { error = "?" }, Json, statusCode: 400);
            return GameServer.Command(line)
                ? Results.Json(new { ok = true }, Json)
                : Results.Json(new { error = Lang.T("Der Server läuft nicht.", "The server is not running.") }, Json, statusCode: 409);
        });

        // ── console ─────────────────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/console", (long? after) => Results.Json(ConsoleBuffer.After(after ?? 0), Json));

        app.MapGet("/api/console/stream", async (HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            long last = 0;
            string? lastId = ctx.Request.Headers["Last-Event-ID"].FirstOrDefault() ?? ctx.Request.Query["after"].FirstOrDefault();
            if (lastId != null) long.TryParse(lastId, out last);
            var ch = ConsoleBuffer.Subscribe();
            var ct = ctx.RequestAborted;
            async Task Send(ConsoleLine l)
            {
                if (l.Seq <= last) return;
                last = l.Seq;
                await ctx.Response.WriteAsync($"id: {l.Seq}\ndata: {JsonSerializer.Serialize(l, Json)}\n\n", ct);
            }
            try
            {
                foreach (var l in ConsoleBuffer.After(last)) await Send(l);
                await ctx.Response.Body.FlushAsync(ct);
                while (!ct.IsCancellationRequested)
                {
                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    wait.CancelAfter(TimeSpan.FromSeconds(15));
                    try
                    {
                        var l = await ch.Reader.ReadAsync(wait.Token);
                        await Send(l);
                        while (ch.Reader.TryRead(out var more)) await Send(more);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        await ctx.Response.WriteAsync(": ping\n\n", ct);
                    }
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            finally { ConsoleBuffer.Unsubscribe(ch); }
        });

        // ── settings ─────────────────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/settings", (string? lang) => Results.Content(SettingsApi.Describe(lang == "de" ? "de" : "en").ToJsonString(), "application/json; charset=utf-8"));

        app.MapPost("/api/settings", async (HttpContext ctx) =>
        {
            var body = await ReadBody(ctx);
            if (body["values"] is not JsonObject values) return Results.Json(new { error = "?" }, Json, statusCode: 400);
            int n = SettingsApi.Save(values);
            Log.Info(Lang.T($"{n} Einstellung(en) gespeichert von {ctx.Items["user"]} - gilt ab dem nächsten Start.", $"{n} setting(s) saved by {ctx.Items["user"]} - applies from the next start."));
            return Results.Json(new { ok = true, saved = n, running = GameServer.Alive }, Json);
        });

        app.MapGet("/api/manager", () => Results.Json(ManagerConfig.Current, new JsonSerializerOptions()));

        app.MapPost("/api/manager", async (HttpContext ctx) =>
        {
            var b = await ReadBody(ctx);
            var c = ManagerConfig.Current;
            bool web = false;
            if (b["WebPort"] is JsonNode wp && int.TryParse(wp.ToString(), out int port) && port is > 0 and < 65536 && port != c.WebPort) { c.WebPort = port; web = true; }
            if (b["WebBind"] is JsonNode wb && wb.ToString().Trim() is { Length: > 0 } bind && bind != c.WebBind)
            {
                if (!IPAddress.TryParse(bind, out _) && bind != "*") return Results.Json(new { error = Lang.T("Ungültige Adresse.", "Invalid address.") }, Json, statusCode: 400);
                c.WebBind = bind;
                web = true;
            }
            if (b["AutoStart"] is JsonNode a) c.AutoStart = a.GetValueKind() == JsonValueKind.True;
            if (b["BackupHour"] is JsonNode bh && int.TryParse(bh.ToString(), out int h)) c.BackupHour = Math.Clamp(h, -1, 23);
            if (b["BackupKeep"] is JsonNode bk && int.TryParse(bk.ToString(), out int k)) c.BackupKeep = Math.Clamp(k, 1, 365);
            if (b["ProtonVersion"] is JsonNode pv) c.ProtonVersion = pv.ToString().Trim();
            if (b["Language"] is JsonNode lg && lg.ToString() is "de" or "en" or "") { c.Language = lg.ToString(); Lang.Init(c.Language); }
            ManagerConfig.Save();
            return Results.Json(new { ok = true, restartManager = web }, Json);
        });

        // ── installation, updates ───────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/job", () => Results.Json(JobJson(Jobs.Current), Json));

        app.MapPost("/api/install/game", async (HttpContext ctx) =>
        {
            var b = await ReadBody(ctx);
            string user = ((string?)b["user"] ?? ManagerConfig.Current.SteamUser).Trim();
            string? pass = (string?)b["password"], code = (string?)b["code"];
            var job = Jobs.Start("game", Lang.T("Spiel installieren / aktualisieren (SteamCMD)", "Install / update the game (SteamCMD)"),
                                 (j, ct) => SteamCmd.InstallGame(j, user, pass, code, ct));
            return job == null ? Busy() : Results.Json(JobJson(job), Json);
        });

        app.MapPost("/api/install/proton", async (HttpContext ctx) =>
        {
            if (OperatingSystem.IsWindows()) return Results.Json(new { error = "Windows" }, Json, statusCode: 400);
            var b = await ReadBody(ctx);
            if (b["version"] is JsonNode v) { ManagerConfig.Current.ProtonVersion = v.ToString().Trim(); ManagerConfig.Save(); }
            var job = Jobs.Start("proton", Lang.T("Proton GE installieren", "Install Proton GE"), (j, ct) => ProtonGE.Install(j, ManagerConfig.Current.ProtonVersion, ct));
            return job == null ? Busy() : Results.Json(JobJson(job), Json);
        });

        app.MapPost("/api/install/files", () =>
        {
            var job = Jobs.Start("files", Lang.T("Server-Dateien aktualisieren (GitHub)", "Update the server files (GitHub)"), Releases.UpdateServerFiles);
            return job == null ? Busy() : Results.Json(JobJson(job), Json);
        });

        app.MapPost("/api/install/check", async () =>
        {
            await Releases.Check(CancellationToken.None);
            return Results.Content(Status().ToJsonString(), "application/json; charset=utf-8");
        });

        // ── backups ──────────────────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/backups", () => Results.Json(Backups.List().Select(f => new { name = f.Name, size = f.Length, time = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds() }), Json));

        app.MapPost("/api/backups", () =>
        {
            var job = Jobs.Start("backup", Lang.T("Backup der Spielstände", "Backup of the saves"), Backups.Create);
            return job == null ? Busy() : Results.Json(JobJson(job), Json);
        });

        app.MapPost("/api/backups/restore", async (HttpContext ctx) =>
        {
            var b = await ReadBody(ctx);
            string? file = Backups.Find((string?)b["name"] ?? "");
            if (file == null) return Results.NotFound();
            var job = Jobs.Start("restore", Lang.T($"Backup wiederherstellen: {Path.GetFileName(file)}", $"Restore backup: {Path.GetFileName(file)}"), (j, ct) => Backups.Restore(j, file, ct));
            return job == null ? Busy() : Results.Json(JobJson(job), Json);
        });

        app.MapGet("/api/backups/file/{name}", (string name) =>
        {
            string? file = Backups.Find(name);
            return file == null ? Results.NotFound() : Results.File(file, "application/zip", name);
        });

        app.MapPost("/api/backups/delete", async (HttpContext ctx) =>
        {
            var b = await ReadBody(ctx);
            string? file = Backups.Find((string?)b["name"] ?? "");
            if (file == null) return Results.NotFound();
            File.Delete(file);
            return Results.Json(new { ok = true }, Json);
        });

        // ── players, logs ────────────────────────────────────────────────────────────────────────────────────────
        app.MapGet("/api/players", () =>
        {
            JsonNode? ledger = null;
            try { if (File.Exists(Paths.PlayersJson)) ledger = JsonNode.Parse(File.ReadAllText(Paths.PlayersJson)); } catch { }
            var rows = new JsonArray();
            if (ledger is JsonArray arr)
                foreach (var r in arr.OfType<JsonObject>())
                    rows.Add(new JsonObject
                    {
                        ["name"] = (string?)r["Name"] ?? "?",
                        ["hours"] = Math.Round(((double?)r["OnlineSeconds"] ?? 0) / 3600.0, 1),
                        ["sessions"] = (int?)r["Sessions"] ?? 0,
                        ["firstSeen"] = (long?)r["FirstSeenUnix"] ?? 0,
                        ["lastSeen"] = (long?)r["LastSeenUnix"] ?? 0,
                    });
            return Results.Content(new JsonObject { ["online"] = GameServer.Status?["players"]?.DeepClone() ?? new JsonArray(), ["all"] = rows }.ToJsonString(), "application/json; charset=utf-8");
        });

        app.MapGet("/api/logs/{name}", (string name) =>
        {
            string? file = name switch
            {
                "console" => Paths.ConsoleLog,
                "manager" => Paths.ManagerLog,
                "events" => Paths.EventsLog,
                "preloader" => Paths.PreloaderLog,
                _ => null,
            };
            if (file == null || !File.Exists(file)) return Results.NotFound();
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long take = Math.Min(fs.Length, 4_000_000);
            fs.Seek(-take, SeekOrigin.End);
            var buf = new byte[take];
            fs.ReadExactly(buf);
            return Results.File(buf, "text/plain; charset=utf-8", Path.GetFileName(file));
        });
    }

    internal static JsonObject Status()
    {
        var st = GameServer.Status;
        string key = "";
        try { key = ServerKey.Current(); } catch { }
        var job = Jobs.Current;
        var backups = Backups.List();
        var game = new JsonObject
        {
            ["installed"] = GameServer.Installed,
            ["state"] = GameServer.State.ToString().ToLowerInvariant(),
            ["detail"] = GameServer.Detail,
            ["pid"] = GameServer.Pid,
            ["startedAt"] = GameServer.Alive ? new DateTimeOffset(GameServer.StartedAt).ToUnixTimeSeconds() : 0,
            ["lastExit"] = GameServer.LastExit,
            ["server"] = st?.DeepClone(),
        };
        var conf = ServerJson.Read();
        return new JsonObject
        {
            ["manager"] = new JsonObject
            {
                ["version"] = Program.Version,
                ["platform"] = Platform.Describe(),
                ["windows"] = OperatingSystem.IsWindows(),
                ["docker"] = Paths.InDocker,
                ["data"] = Paths.Data,
                ["urls"] = new JsonArray(Credentials.Urls().Select(u => (JsonNode)JsonValue.Create(u)!).ToArray()),
                ["autoStart"] = ManagerConfig.Current.AutoStart,
            },
            ["game"] = game,
            ["port"] = (int?)conf["HostPort"] ?? 7777,
            ["world"] = (string?)conf["DedicatedSession"] ?? "",
            ["newWorld"] = (string?)conf["DedicatedNewWorld"] ?? "",
            ["newGame"] = (string?)conf["DedicatedNewGame"] ?? "",
            ["hasWorld"] = GameServer.HasWorld(),
            ["key"] = key,
            ["files"] = new JsonObject
            {
                ["installed"] = ServerFiles.Installed,
                ["version"] = ServerFiles.InstalledVersion,
                ["package"] = ServerFiles.PackageVersion,
                ["latest"] = Releases.LatestTag,
                ["latestPage"] = Releases.LatestPage,
                ["updateAvailable"] = Releases.UpdateAvailable,
                ["checkError"] = Releases.CheckError,
                ["complete"] = ServerFiles.Complete,
                ["extraMods"] = new JsonArray(ExtraMods.List().Select(m => (JsonNode)JsonValue.Create(m.Folder)!).ToArray()),
                ["modsDir"] = Paths.Mods,
            },
            ["steam"] = new JsonObject { ["steamcmd"] = SteamCmd.Installed, ["user"] = ManagerConfig.Current.SteamUser },
            ["proton"] = new JsonObject
            {
                ["needed"] = !OperatingSystem.IsWindows(),
                ["installed"] = ProtonGE.Installed,
                ["version"] = ProtonGE.Version,
                ["wanted"] = ManagerConfig.Current.ProtonVersion,
                ["prefix"] = Directory.Exists(Path.Combine(Paths.Prefix, "pfx", "drive_c")),
            },
            ["job"] = JobJson(job),
            ["backups"] = new JsonObject { ["count"] = backups.Count, ["last"] = backups.FirstOrDefault()?.Name ?? "", ["hour"] = ManagerConfig.Current.BackupHour, ["keep"] = ManagerConfig.Current.BackupKeep },
        };
    }

    private static JsonObject? JobJson(Job? j) => j == null ? null : new JsonObject
    {
        ["id"] = j.Id, ["kind"] = j.Kind, ["title"] = j.Title, ["state"] = j.State, ["message"] = j.Message,
        ["progress"] = j.Progress, ["started"] = new DateTimeOffset(j.Started).ToUnixTimeSeconds(),
        ["ended"] = j.Ended is DateTime e ? new DateTimeOffset(e).ToUnixTimeSeconds() : null,
        ["lines"] = new JsonArray(j.Lines().TakeLast(60).Select(l => (JsonNode)JsonValue.Create(l)!).ToArray()),
    };

    private static IResult Busy() => Results.Json(new { error = Lang.T("Eine andere Aufgabe läuft noch.", "Another task is still running.") }, Json, statusCode: 409);

    private static string Do(Action a) { a(); return ""; }

    private static string Clip(string s) => s.Length > 40 ? s[..40] : s;

    private static async Task<JsonObject> ReadBody(HttpContext ctx)
    {
        try
        {
            if (ctx.Request.ContentLength is > 65536) return new JsonObject();
            using var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            return JsonNode.Parse(await sr.ReadToEndAsync()) as JsonObject ?? new JsonObject();
        }
        catch { return new JsonObject(); }
    }
}
