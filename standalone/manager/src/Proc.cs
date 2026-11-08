using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>Running a helper program (tar, Proton, SteamCMD) with its output going into a job.</summary>
internal static class Proc
{
    internal static async Task<int> Run(string file, IEnumerable<string> args, Job? job, CancellationToken ct,
                                        Action<IDictionary<string, string?>>? env = null, string? workDir = null, Action<string>? onLine = null)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (workDir != null) psi.WorkingDirectory = workDir;
        env?.Invoke(psi.Environment);
        using var p = new Process { StartInfo = psi };
        void Line(string? l)
        {
            if (string.IsNullOrWhiteSpace(l)) return;
            if (onLine != null) onLine(l);
            else job?.Line(l);
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data);
        p.Start();
        p.StandardInput.Close();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return p.ExitCode;
    }
}

/// <summary>Downloads and GitHub's API.</summary>
internal static class Net
{
    internal static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GoingPublicServer", Program.Version));
        return c;
    }

    internal static async Task<JsonNode> GetJson(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var res = await Http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"{url}: HTTP {(int)res.StatusCode}");
        return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct)) ?? throw new InvalidOperationException($"{url}: empty answer");
    }

    internal static async Task Download(string url, string file, Job? job, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"{url}: HTTP {(int)res.StatusCode}");
        long total = res.Content.Headers.ContentLength ?? -1, done = 0;
        string tmp = file + ".part";
        await using (var src = await res.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(tmp))
        {
            var buf = new byte[1 << 16];
            var last = DateTime.MinValue;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (job != null && total > 0 && DateTime.UtcNow - last > TimeSpan.FromMilliseconds(500))
                {
                    last = DateTime.UtcNow;
                    job.Progress = (double)done / total;
                }
            }
        }
        File.Move(tmp, file, true);
        if (job != null) job.Line($"{Path.GetFileName(file)}: {done / 1048576.0:0.0} MB");
    }
}
