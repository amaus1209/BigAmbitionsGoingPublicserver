"use strict";
// STANDALONE-1: the web interface of Going Public Server - login, overview, console, settings, players, installation,
// backups, account. Plain JS, no external files. Every change goes through api() with the header X-GPS.

const T = {
  de: {
    loginHint: "Weboberfläche des Servers - melde dich mit den Zugangsdaten an, die beim ersten Start angezeigt wurden.",
    user: "Benutzer", password: "Passwort", login: "Anmelden", logout: "Abmelden", other: "English",
    tDash: "Übersicht", tConsole: "Konsole", tSettings: "Einstellungen", tPlayers: "Spieler", tInstall: "Installation", tBackups: "Backups", tAccount: "Konto",
    st_stopped: "Gestoppt", st_starting: "Startet", st_running: "Läuft", st_stopping: "Stoppt", st_crashed: "Abgestürzt", st_notinstalled: "Nicht installiert",
    ss_loading: "lädt die Welt ...", ss_menu: "im Hauptmenü", ss_lobby: "Lobby", ss_idle: "wartet auf Spieler (Welt pausiert)", ss_running: "Spieler sind da",
    ss_stopping: "speichert und stoppt ...", ss_stopped: "gespeichert, beendet sich", ss_prep: "bereitet den Start vor ...",
    start: "Starten", stop: "Stoppen", restart: "Neu starten", kill: "Hart beenden", save: "Speichern",
    confirmKill: "Den Server sofort beenden - ohne zu speichern?", confirmStop: "Den Server stoppen? Er speichert und sagt es den Spielern.",
    players: "Spieler online", world: "Welt", key: "Server-Schlüssel", port: "Spiel-Port (UDP)", copy: "Kopieren", copied: "Kopiert",
    lastWorld: "(zuletzt gespielte)", ofMax: "von {0}", paused: "Welt pausiert", portNote: "im Router/der Firewall freigeben",
    setupTitle: "Einrichtung", stepGame: "Spiel installieren - mit dem Steam-Konto, das Big Ambitions besitzt (Installation)",
    stepProton: "Proton GE installieren (Installation)", stepWorld: "Welt wählen oder eine neue anlegen (Einstellungen → Server / New world)",
    stepStart: "Server starten", stepPass: "Eigenes Passwort setzen (Konto)", ready: "Alles eingerichtet.",
    joinTitle: "Beitreten", joinHint: "Spieler: Going Public Server im Steam Workshop abonnieren, dann Multiplayer → Join a Game mit der Adresse dieses Rechners, Port {0} und dem Server-Schlüssel {1}.",
    rankingLink: "Ranglisten-Seite für die Spieler öffnen", modVersion: "Mod-Version", uptime: "Läuft seit", gameDay: "Spieltag",
    recent: "Letzte Konsolenzeilen", openConsole: "Ganze Konsole",
    srcGame: "Spiel", srcManager: "Verwaltung", srcJob: "Aufgaben", autoscroll: "Mitlaufen",
    cmdPlaceholder: "Befehl, z. B. status, players, save, say Hallo", send: "Senden",
    settingsHint: "Die Einstellungen stehen in bamp-server.json und gelten ab dem nächsten Start des Servers.",
    discard: "Verwerfen", saveSettings: "Speichern", saveRestart: "Speichern und neu starten",
    savedNext: "Gespeichert - gilt ab dem nächsten Start.", unsaved: "{0} ungespeicherte Änderung(en)", noChanges: "Keine Änderungen.", defaultIs: "Standard: {0}",
    on: "an", offv: "aus",
    online: "Gerade online", nobody: "Niemand online.", actions: "Aktionen", all: "Alle Spieler", hours: "Stunden", sessions: "Sitzungen",
    firstSeen: "Erster Besuch", lastSeen: "Letzter Besuch", ban: "Bannen", unban: "Entbannen",
    chat: "Chat", chatPh: "Nachricht", toAll: "An alle", chatNone: "Noch kein Chat.", msg: "Nachricht", kick: "Kicken", ping: "{0} ms", onFor: "seit {0}",
    msgTo: "Nachricht an {0}:", reasonKick: "{0} kicken – Grund (optional, der Spieler sieht ihn; er darf gleich wieder beitreten):",
    reasonBan: "{0} bannen – Grund (optional, der Spieler sieht ihn):", bans: "Bannliste", bansNone: "Niemand ist gebannt.", bannedOn: "Gebannt am",
    reason: "Grund", how: "Erkannt über", howSteam: "Steam-Konto", howIp: "IP-Adresse", day: "Tag {0}", unbanAll: "Alle entbannen",
    confirmUnban: "Bann von {0} aufheben?", confirmUnbanAll: "Alle Spieler entbannen?", banPh: "Name eines Spielers (auch offline) oder Bann-Schlüssel",
    announce: "Ankündigung", announceBtn: "Ankündigen", noticePh: "Meldung auf dem Bildschirm aller Spieler", restartIn: "Neustart ankündigen", restartOff: "Neustart absagen",
    restartMin: "In wie vielen Minuten neu starten? Die Spieler werden bei 5 min, 1 min, 30 s und 10 s gewarnt.", restartLeft: "Neustart in {0}", stopLeft: "Stopp in {0}",
    gameCard: "Spiel (Big Ambitions)", state: "Zustand", installed: "Installiert", notInstalled: "nicht installiert", yes: "ja", no: "nein",
    steamUser: "Steam-Benutzername", steamPass: "Steam-Passwort", steamPassHint: "nur nötig, wenn SteamCMD noch keine gespeicherte Anmeldung hat",
    guard: "Steam-Guard-Code", guardHint: "aus der Mail oder der Steam-App - nur wenn Steam danach fragt", installGame: "Installieren / Aktualisieren",
    steamNote: "Das Spiel muss mit diesem Steam-Konto gekauft sein (kein anonymer Download). Name und Passwort gehen nur an SteamCMD; gespeichert wird nur der Name, SteamCMD merkt sich die Anmeldung selbst. Ein laufender Server wird dafür gestoppt (er speichert) und danach wieder gestartet.",
    protonCard: "Proton GE (Linux)", protonPh: "leer = neueste, z. B. GE-Proton10-25", installProton: "Installieren / Aktualisieren",
    protonNote: "Proton GE führt das Windows-Spiel unter Linux aus. Ein laufender Server wird dafür gestoppt.",
    filesCard: "Server-Dateien (Mod, Vorlader, Ranglisten-Seite)", filesPackage: "Im Programmpaket", filesLatest: "Neuestes Release",
    updateFiles: "Von GitHub aktualisieren", checkUpdates: "Nach Updates suchen", updateAvailable: "Update verfügbar", upToDate: "aktuell",
    filesNote: "Die Spieler brauchen genau dieselbe Version aus dem Steam Workshop - der Server weist andere ab. Die Verwaltung selbst kommt als neues Programmpaket.",
    modsCard: "Zusätzliche Mods", modsNone: "Keine - nur Going Public Server.",
    modsNote: "Weitere Mods als Ordner nach {0} legen (wie in ModsLocal; ein Workshop-Mod heißt wie seine Workshop-Nummer) und den Server neu starten. Alle Spieler brauchen sie ebenfalls.",
    job_running: "läuft", job_done: "fertig", "job_failed": "fehlgeschlagen", "job_needs-code": "Code nötig", "job_needs-password": "Passwort nötig",
    backupsTitle: "Backups der Spielstände", backupNow: "Backup jetzt", restore: "Wiederherstellen", download: "Herunterladen", del: "Löschen",
    confirmRestore: "{0} wiederherstellen? Der Server wird gestoppt; die jetzigen Spielstände werden daneben aufgehoben.", confirmDelete: "{0} löschen?",
    noBackups: "Noch keine Backups.", schedule: "Automatisches Backup", backupHour: "Täglich um", keep: "Aufheben (Anzahl)", apply: "Übernehmen",
    hourOff: "aus", size: "Größe", date: "Datum", file: "Datei",
    pwTitle: "Passwort ändern", pwCurrent: "Aktuelles Passwort", pwNew: "Neues Passwort (mindestens 10 Zeichen)", pwRepeat: "Neues Passwort wiederholen",
    pwChange: "Ändern", pwMismatch: "Die beiden neuen Passwörter sind verschieden.", pwChanged: "Passwort geändert.",
    mgrTitle: "Verwaltung", webPort: "Port der Weboberfläche", webBind: "Adresse (0.0.0.0 = alle)", autoStart: "Server mit der Verwaltung starten",
    langLabel: "Sprache der Verwaltung (Log, Meldungen)", langAuto: "automatisch", mgrRestartNote: "Port und Adresse gelten nach einem Neustart der Verwaltung.",
    mgrSaved: "Übernommen.", mgrSavedRestart: "Übernommen - Port/Adresse gelten nach einem Neustart der Verwaltung.",
    logsTitle: "Logs (Download)", logConsole: "Spiel-Log (Konsole)", logManager: "Verwaltung", logEvents: "Ereignisse", logPreloader: "Vorlader",
    about: "Going Public Server {0} · {1} · Daten: {2}", started: "Gestartet.", stopping: "Wird gestoppt ...", sent: "Gesendet.", saveSent: "Der Server speichert.",
    never: "-", days: "{0} T {1} h", hm: "{0} h {1} min", mins: "{0} min",
  },
  en: {
    loginHint: "The server's web interface - log in with the access data shown at the first start.",
    user: "User", password: "Password", login: "Log in", logout: "Log out", other: "Deutsch",
    tDash: "Overview", tConsole: "Console", tSettings: "Settings", tPlayers: "Players", tInstall: "Installation", tBackups: "Backups", tAccount: "Account",
    st_stopped: "Stopped", st_starting: "Starting", st_running: "Running", st_stopping: "Stopping", st_crashed: "Crashed", st_notinstalled: "Not installed",
    ss_loading: "loading the world ...", ss_menu: "in the main menu", ss_lobby: "lobby", ss_idle: "waiting for players (world paused)", ss_running: "players are on",
    ss_stopping: "saving and stopping ...", ss_stopped: "saved, ending", ss_prep: "preparing the start ...",
    start: "Start", stop: "Stop", restart: "Restart", kill: "Kill", save: "Save",
    confirmKill: "End the server now - without saving?", confirmStop: "Stop the server? It saves and tells the players.",
    players: "Players online", world: "World", key: "Server key", port: "Game port (UDP)", copy: "Copy", copied: "Copied",
    lastWorld: "(played last)", ofMax: "of {0}", paused: "world paused", portNote: "open it in the router / firewall",
    setupTitle: "Setup", stepGame: "Install the game - with the Steam account that owns Big Ambitions (Installation)",
    stepProton: "Install Proton GE (Installation)", stepWorld: "Choose a world or make a new one (Settings → Server / New world)",
    stepStart: "Start the server", stepPass: "Set your own password (Account)", ready: "All set up.",
    joinTitle: "Joining", joinHint: "Players: subscribe to Going Public Server in the Steam Workshop, then Multiplayer → Join a Game with this machine's address, port {0} and the server key {1}.",
    rankingLink: "Open the ranking page for the players", modVersion: "Mod version", uptime: "Running for", gameDay: "Game day",
    recent: "Last console lines", openConsole: "Whole console",
    srcGame: "Game", srcManager: "Manager", srcJob: "Tasks", autoscroll: "Follow",
    cmdPlaceholder: "Command, e.g. status, players, save, say Hello", send: "Send",
    settingsHint: "The settings are kept in bamp-server.json and apply from the server's next start.",
    discard: "Discard", saveSettings: "Save", saveRestart: "Save and restart",
    savedNext: "Saved - applies from the next start.", unsaved: "{0} unsaved change(s)", noChanges: "No changes.", defaultIs: "Default: {0}",
    on: "on", offv: "off",
    online: "Online now", nobody: "Nobody online.", actions: "Actions", all: "All players", hours: "Hours", sessions: "Sessions",
    firstSeen: "First visit", lastSeen: "Last visit", ban: "Ban", unban: "Unban",
    chat: "Chat", chatPh: "Message", toAll: "To everyone", chatNone: "No chat yet.", msg: "Message", kick: "Kick", ping: "{0} ms", onFor: "on for {0}",
    msgTo: "Message to {0}:", reasonKick: "Kick {0} - reason (optional, the player sees it; he may join again at once):",
    reasonBan: "Ban {0} - reason (optional, the player sees it):", bans: "Ban list", bansNone: "Nobody is banned.", bannedOn: "Banned",
    reason: "Reason", how: "Recognised by", howSteam: "Steam account", howIp: "IP address", day: "day {0}", unbanAll: "Unban everyone",
    confirmUnban: "Lift the ban of {0}?", confirmUnbanAll: "Unban every player?", banPh: "A player's name (offline too) or a ban key",
    announce: "Announcement", announceBtn: "Announce", noticePh: "A message on every player's screen", restartIn: "Announce a restart", restartOff: "Call off the restart",
    restartMin: "Restart in how many minutes? The players are warned at 5 min, 1 min, 30 s and 10 s.", restartLeft: "restart in {0}", stopLeft: "stop in {0}",
    gameCard: "Game (Big Ambitions)", state: "State", installed: "Installed", notInstalled: "not installed", yes: "yes", no: "no",
    steamUser: "Steam user name", steamPass: "Steam password", steamPassHint: "only needed while SteamCMD has no saved login",
    guard: "Steam Guard code", guardHint: "from the mail or the Steam app - only when Steam asks for it", installGame: "Install / update",
    steamNote: "The game has to be owned by this Steam account (no anonymous download). Name and password go only to SteamCMD; only the name is kept, SteamCMD remembers the login itself. A running server is stopped for it (it saves) and started again afterwards.",
    protonCard: "Proton GE (Linux)", protonPh: "empty = newest, e.g. GE-Proton10-25", installProton: "Install / update",
    protonNote: "Proton GE runs the Windows game on Linux. A running server is stopped for it.",
    filesCard: "Server files (mod, preloader, ranking page)", filesPackage: "In the program package", filesLatest: "Newest release",
    updateFiles: "Update from GitHub", checkUpdates: "Check for updates", updateAvailable: "update available", upToDate: "up to date",
    filesNote: "The players need exactly the same version from the Steam Workshop - the server refuses others. The manager itself comes as a new program package.",
    modsCard: "Extra mods", modsNone: "None - only Going Public Server.",
    modsNote: "Put more mods as folders into {0} (as in ModsLocal; a Workshop mod is named after its Workshop number) and restart the server. Every player needs them too.",
    job_running: "running", job_done: "done", "job_failed": "failed", "job_needs-code": "code needed", "job_needs-password": "password needed",
    backupsTitle: "Backups of the saves", backupNow: "Back up now", restore: "Restore", download: "Download", del: "Delete",
    confirmRestore: "Restore {0}? The server is stopped; the current saves are kept next to them.", confirmDelete: "Delete {0}?",
    noBackups: "No backups yet.", schedule: "Automatic backup", backupHour: "Daily at", keep: "Keep (count)", apply: "Apply",
    hourOff: "off", size: "Size", date: "Date", file: "File",
    pwTitle: "Change password", pwCurrent: "Current password", pwNew: "New password (at least 10 characters)", pwRepeat: "Repeat the new password",
    pwChange: "Change", pwMismatch: "The two new passwords differ.", pwChanged: "Password changed.",
    mgrTitle: "Manager", webPort: "Web interface port", webBind: "Address (0.0.0.0 = all)", autoStart: "Start the server with the manager",
    langLabel: "Language of the manager (log, messages)", langAuto: "automatic", mgrRestartNote: "Port and address apply after a restart of the manager.",
    mgrSaved: "Applied.", mgrSavedRestart: "Applied - port / address apply after a restart of the manager.",
    logsTitle: "Logs (download)", logConsole: "Game log (console)", logManager: "Manager", logEvents: "Events", logPreloader: "Preloader",
    about: "Going Public Server {0} · {1} · data: {2}", started: "Started.", stopping: "Stopping ...", sent: "Sent.", saveSent: "The server is saving.",
    never: "-", days: "{0} d {1} h", hm: "{0} h {1} min", mins: "{0} min",
  },
};

let lang = "de";
try { lang = localStorage.getItem("gps-lang") || ((navigator.language || "en").toLowerCase().startsWith("de") ? "de" : "en"); } catch (e) { }
const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];
function t(k, ...a) {
  let s = (T[lang] && T[lang][k]) ?? T.en[k] ?? k;
  a.forEach((v, i) => { s = s.split("{" + i + "}").join(String(v)); });
  return s;
}
function el(tag, props = {}, ...kids) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (k === "class") e.className = v;
    else if (k === "text") e.textContent = v;
    else if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
    else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? "" : v);
  }
  for (const c of kids) if (c !== null && c !== undefined) e.append(c instanceof Node ? c : document.createTextNode(String(c)));
  return e;
}
const fmtNum = (v, d = 0) => v == null || isNaN(v) ? "-" : Number(v).toLocaleString(lang === "de" ? "de-DE" : "en-GB", { maximumFractionDigits: d, minimumFractionDigits: d });
const fmtDate = u => u ? new Date(u * 1000).toLocaleString(lang === "de" ? "de-DE" : "en-GB", { dateStyle: "short", timeStyle: "short" }) : t("never");
const fmtClock = ms => new Date(ms).toLocaleTimeString(lang === "de" ? "de-DE" : "en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
function fmtSpan(sec) {
  if (!sec || sec < 0) return "-";
  const d = Math.floor(sec / 86400), h = Math.floor(sec % 86400 / 3600), m = Math.floor(sec % 3600 / 60);
  return d > 0 ? t("days", d, h) : h > 0 ? t("hm", h, m) : t("mins", m);
}

// ── API ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
async function api(path, body) {
  const opt = { method: body === undefined ? "GET" : "POST", headers: { "X-GPS": "1" }, credentials: "same-origin" };
  if (body !== undefined) { opt.headers["Content-Type"] = "application/json"; opt.body = JSON.stringify(body); }
  const r = await fetch(path, opt);
  let data = null;
  try { data = await r.json(); } catch (e) { }
  if (r.status === 401 && path !== "/api/login") { showLogin(); throw new Error("login"); }
  if (!r.ok) throw new Error((data && data.error) || ("HTTP " + r.status));
  return data;
}
let toastTimer = 0;
function toast(msg, bad) {
  const b = $("#toast");
  b.textContent = msg;
  b.classList.toggle("bad", !!bad);
  b.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { b.hidden = true; }, bad ? 6000 : 3000);
}
async function act(fn, okMsg) {
  try { const r = await fn(); if (okMsg) toast(okMsg); return r; }
  catch (e) { if (e.message !== "login") toast(e.message, true); return null; }
}

// ── texts ────────────────────────────────────────────────────────────────────────────────────────────────────────────
function applyTexts() {
  document.documentElement.lang = lang;
  $$("[data-t]").forEach(e => { e.textContent = t(e.dataset.t); });
  $$("[data-tp]").forEach(e => { e.placeholder = t(e.dataset.tp); });
  $$("[data-lang]").forEach(b => { b.textContent = t("other"); });
}
function switchLang() {
  lang = lang === "de" ? "en" : "de";
  try { localStorage.setItem("gps-lang", lang); } catch (e) { }
  applyTexts();
  renderStatus();
  renderConsole(true);
  if (settings) loadSettings();
  if (current === "players") loadPlayers();
  if (current === "backups") loadBackups();
}

// ── login ────────────────────────────────────────────────────────────────────────────────────────────────────────────
let me = null;
function showLogin() {
  me = null;
  $("#app").hidden = true;
  $("#login").hidden = false;
  stopStream();
  setTimeout(() => ($("#lUser").value ? $("#lPass") : $("#lUser")).focus(), 50);
}
async function showApp(user) {
  me = user;
  $("#login").hidden = true;
  $("#app").hidden = false;
  $("#pwUser").value = user;
  startStream();
  await refresh();
  const tab = (location.hash || "").replace("#", "");
  go(T.en["t" + tab.charAt(0).toUpperCase() + tab.slice(1)] ? tab : "dash");
}

// ── tabs ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
let current = "dash";
function go(tab) {
  current = tab;
  $$("#tabs button").forEach(b => b.classList.toggle("on", b.dataset.tab === tab));
  $$("main > section").forEach(s => { s.hidden = s.id !== "tab-" + tab; });
  history.replaceState(null, "", "#" + tab);
  if (tab === "settings" && !settings) loadSettings();
  if (tab === "players") loadPlayers();
  if (tab === "backups") loadBackups();
  if (tab === "account") loadManager();
  if (tab === "console") { renderConsole(true); $("#cmdIn").focus(); }
}

// ── status ───────────────────────────────────────────────────────────────────────────────────────────────────────────
let S = null;
async function refresh() {
  try { S = await api("/api/status"); renderStatus(); } catch (e) { }
}
function stateOf(s) {
  if (!s) return { key: "st_stopped", cls: "" };
  const g = s.game;
  if (!g.installed && g.state === "stopped") return { key: "st_notinstalled", cls: "" };
  return {
    stopped: { key: "st_stopped", cls: "" }, starting: { key: "st_starting", cls: "warn" }, running: { key: "st_running", cls: "ok" },
    stopping: { key: "st_stopping", cls: "warn" }, crashed: { key: "st_crashed", cls: "err" },
  }[g.state] || { key: "st_stopped", cls: "" };
}
function renderStatus() {
  if (!S) return;
  const g = S.game, srv = g.server || {}, st = stateOf(S), alive = g.pid > 0;
  for (const [dot, txt] of [[$("#topBadge .dot"), $("#topBadge span:last-child")], [$("#dDot"), $("#dState")]]) {
    dot.className = "dot " + st.cls;
    txt.textContent = t(st.key);
  }
  let detail = g.detail || "";
  if (!detail && alive && srv.state) detail = t("ss_" + srv.state);
  if (!detail && g.state === "starting") detail = t("ss_prep");
  $("#dDetail").textContent = detail;
  $("#bStart").disabled = !g.installed || alive || g.state === "starting" || g.state === "stopping";
  $("#bStop").disabled = !(alive || g.state === "starting" || g.state === "crashed");
  $("#bRestart").disabled = !g.installed || g.state === "stopping";
  $("#bSave").disabled = !alive || !["idle", "running"].includes(srv.state);
  $("#bKill").disabled = !alive;

  const players = srv.players || [];
  $("#vPlayers").textContent = alive ? fmtNum(players.length) : "-";
  $("#nPlayers").textContent = players.length ? players.join(", ") : (alive && srv.idlePause ? t("paused") : "");
  const chosen = S.world && S.world !== "-" ? S.world : "";
  $("#vWorld").textContent = srv.session || chosen || (S.newWorld && S.newGame ? S.newWorld : t("lastWorld"));
  $("#nWorld").textContent = srv.gameDay ? t("gameDay") + " " + srv.gameDay : "";
  $("#vKey").textContent = S.key || "-";
  $("#vPort").textContent = srv.port || S.port;
  $("#nPort").textContent = t("portNote");
  $("#vMod").textContent = srv.modVersion || S.files.version || "-";
  $("#vUptime").textContent = alive && g.startedAt ? fmtSpan(Date.now() / 1000 - g.startedAt) : "-";
  $("#vDay").textContent = srv.gameDay || "-";
  $("#joinHint").textContent = t("joinHint", srv.port || S.port, S.key || "-");
  $("#rankLink").href = "/monitor/index.html" + (S.key ? "#key=" + encodeURIComponent(S.key) : "");

  // setup steps
  const steps = [];
  steps.push({ text: t("stepGame"), done: g.installed, tab: "install" });
  if (S.proton.needed) steps.push({ text: t("stepProton"), done: S.proton.installed, tab: "install" });
  steps.push({ text: t("stepWorld"), done: !!(srv.session || S.hasWorld || S.newGame), tab: "settings" });
  steps.push({ text: t("stepStart"), done: alive && ["idle", "running"].includes(srv.state), tab: "dash" });
  const ol = $("#steps");
  ol.textContent = "";
  let next = false;
  steps.forEach((s, i) => {
    const cls = s.done ? "done" : (!next ? (next = true, "next") : "");
    ol.append(el("li", { class: cls }, el("span", { class: "mark", text: s.done ? "✓" : String(i + 1) }),
      s.done || s.tab === "dash" ? el("span", { text: s.text }) : el("button", { class: "link", text: s.text, onclick: () => go(s.tab) })));
  });
  $("#setupCard").classList.toggle("done", steps.every(s => s.done));

  // installation tab
  $("#iGame").textContent = g.installed ? t("installed") : t("notInstalled");
  $("#iSteamcmd").textContent = S.steam.steamcmd ? t("installed") : t("notInstalled");
  if (!$("#gUser").value && S.steam.user) $("#gUser").value = S.steam.user;
  $("#protonCard").hidden = !S.proton.needed;
  $("#iProton").textContent = S.proton.installed ? (S.proton.version || t("yes")) : t("notInstalled");
  if (document.activeElement !== $("#pVersion")) $("#pVersion").value = S.proton.wanted || "";
  $("#iFiles").textContent = S.files.installed || "-";
  $("#iPackage").textContent = S.files.package || "-";
  $("#iLatest").textContent = S.files.latest ? S.files.latest + " - " + (S.files.updateAvailable ? t("updateAvailable") : t("upToDate")) : (S.files.checkError || "-");
  $("#bUpdateFiles").classList.toggle("primary", !!S.files.updateAvailable);
  const mods = S.files.extraMods || [];
  $("#iMods").textContent = mods.length ? mods.join(", ") : t("modsNone");
  $("#iModsNote").textContent = t("modsNote", S.files.modsDir);
  $("#about").textContent = t("about", S.manager.version, S.manager.platform, S.manager.data);
  renderJob(S.job);
}

// ── job ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
let jobTimer = 0;
function renderJob(j) {
  const card = $("#jobCard");
  if (!j) { card.hidden = true; return; }
  card.hidden = false;
  card.className = "card job " + j.state;
  $("#jobTitle").textContent = j.title;
  $("#jobState").textContent = t("job_" + j.state);
  $("#jobMsg").textContent = j.message || "";
  const bar = $("#jobBar");
  bar.hidden = j.state !== "running";
  bar.classList.toggle("indet", !(j.progress >= 0));
  $("i", bar).style.width = j.progress >= 0 ? Math.round(j.progress * 100) + "%" : "";
  const lines = $("#jobLines");
  lines.textContent = (j.lines || []).join("\n");
  lines.scrollTop = lines.scrollHeight;
  clearTimeout(jobTimer);
  if (j.state === "running") jobTimer = setTimeout(pollJob, 1000);
}
async function pollJob() {
  try {
    const j = await api("/api/job");
    if (S) S.job = j;
    renderJob(j);
    if (j && j.state !== "running") refresh();
  } catch (e) { }
}
function startJob(path, body, confirmText) {
  if (confirmText && !confirm(confirmText)) return;
  act(async () => {
    const j = await api(path, body || {});
    renderJob(j);
    if (current !== "install" && !["backup"].includes(j.kind)) go("install");
  });
}

// ── console ──────────────────────────────────────────────────────────────────────────────────────────────────────────
const lines = [];
let lastSeq = 0, es = null, esRetry = 0;
function startStream() {
  stopStream();
  es = new EventSource("/api/console/stream?after=" + lastSeq);
  es.onmessage = ev => {
    try {
      const l = JSON.parse(ev.data);
      if (l.seq <= lastSeq) return;
      lastSeq = l.seq;
      adminLine(l);   // ADMIN-1: the server's answers and chat for the players tab
      lines.push(l);
      if (lines.length > 3000) lines.splice(0, lines.length - 3000);
      queueRender();
    } catch (e) { }
  };
  es.onopen = () => { esRetry = 0; };
  es.onerror = () => {
    if (es && es.readyState === EventSource.CLOSED) {
      stopStream();
      setTimeout(() => { if (me) startStream(); }, Math.min(30000, 1000 * Math.pow(2, esRetry++)));
    }
  };
}
function stopStream() { if (es) { es.close(); es = null; } }
let renderQueued = false;
function queueRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; renderConsole(false); });
}
const isData = l => /\[BAMP-HEADLESS\] DATA /.test(l.text);   // ADMIN-1: the players tab's data - not for reading
const srcOk = l => {
  if (isData(l)) return false;
  const s = l.src || "";
  if (s === "game") return $("#fGame").checked;
  if (s.startsWith("manager") || s === "input") return $("#fManager").checked;
  return $("#fJob").checked;
};
function lineEl(l) {
  const e = el("span", { class: "l s-" + l.src }, el("span", { class: "t", text: fmtClock(l.time) }), l.text);
  if (/\b(FATAL|ERROR|Exception)\b/.test(l.text)) e.classList.add("hl-err");
  else if (/\[BAMP-HEADLESS\] (READY|JOIN)/.test(l.text)) e.classList.add("hl-ok");
  return e;
}
let drawnSeq = 0;
function renderConsole(full) {
  const box = $("#console"), mini = $("#miniConsole");
  if (full) { box.textContent = ""; drawnSeq = 0; }
  const atBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 40;
  const frag = document.createDocumentFragment();
  for (const l of lines) if (l.seq > drawnSeq && srcOk(l)) frag.append(lineEl(l));
  drawnSeq = lastSeq;
  box.append(frag);
  while (box.childNodes.length > 3000) box.firstChild.remove();
  if ($("#fScroll").checked && (atBottom || full)) box.scrollTop = box.scrollHeight;
  mini.textContent = "";
  for (const l of lines.filter(x => (x.src === "game" || x.src.startsWith("manager")) && !isData(x)).slice(-12)) mini.append(lineEl(l));
  mini.scrollTop = mini.scrollHeight;
}
async function sendCommand(line) {
  line = line.trim();
  if (!line) return;
  await act(() => api("/api/server/command", { line }));
}

// ── settings ─────────────────────────────────────────────────────────────────────────────────────────────────────────
let settings = null, setGroup = "", edits = {};
async function loadSettings() {
  const r = await act(() => api("/api/settings?lang=" + lang));
  if (!r) return;
  settings = r;
  edits = {};
  if (!setGroup || !r.groups.some(g => g.id === setGroup)) setGroup = r.groups.length ? r.groups[0].id : "";
  renderSettings();
}
function fieldValue(f) { return Object.prototype.hasOwnProperty.call(edits, f.key) ? edits[f.key] : f.value; }
function same(a, b) { return String(a ?? "") === String(b ?? ""); }
function renderSettings() {
  const tabs = $("#setTabs"), box = $("#setFields");
  tabs.textContent = "";
  box.textContent = "";
  for (const g of settings.groups)
    tabs.append(el("button", { type: "button", class: g.id === setGroup ? "on" : "", text: g.name, onclick: () => { setGroup = g.id; renderSettings(); } }));
  const g = settings.groups.find(x => x.id === setGroup);
  if (g) for (const f of g.fields) box.append(fieldEl(f));
  renderSaveBar();
}
function fieldEl(f) {
  let input;
  const v = fieldValue(f);
  const set = val => {
    if (same(val, f.value)) delete edits[f.key]; else edits[f.key] = val;
    wrap.classList.toggle("changed", Object.prototype.hasOwnProperty.call(edits, f.key));
    renderSaveBar();
  };
  if (f.type === "checkbox") {
    input = el("input", { type: "checkbox", id: "f-" + f.key });
    input.checked = v === true || String(v).toLowerCase() === "true";
    input.addEventListener("change", () => set(input.checked));
  } else if (f.type === "enum") {
    input = el("select", { id: "f-" + f.key });
    let found = false;
    for (const [val, label] of f.choices) {
      const o = el("option", { value: val, text: label });
      if (same(val, v)) { o.selected = true; found = true; }
      input.append(o);
    }
    if (!found && v !== "" && v != null) input.prepend(el("option", { value: String(v), text: String(v), selected: true }));
    input.addEventListener("change", () => set(input.value));
  } else {
    input = el("input", { id: "f-" + f.key, type: f.type === "number" ? "number" : "text", step: f.type === "number" ? "any" : null, placeholder: f.placeholder || null, spellcheck: "false" });
    input.value = v ?? "";
    input.addEventListener("input", () => set(f.type === "number" && input.value !== "" ? Number(input.value) : input.value));
  }
  const def = f.default !== "" && f.type !== "checkbox" && f.type !== "enum" ? el("div", { class: "fd def", text: t("defaultIs", f.default) }) : null;
  const wrap = el("div", { class: "field" + (Object.prototype.hasOwnProperty.call(edits, f.key) ? " changed" : "") },
    el("div", {}, el("label", { class: "fl", for: "f-" + f.key, text: f.label }), el("div", { class: "fd", text: f.desc }), def),
    el("div", { class: "fi" }, input));
  return wrap;
}
function renderSaveBar() {
  const n = Object.keys(edits).length;
  $("#setState").textContent = n ? t("unsaved", n) : t("noChanges");
  $("#setSave").disabled = $("#setReset").disabled = n === 0;
  $("#setSaveRestart").disabled = n === 0 && !(S && S.game.pid > 0);
}
async function saveSettings(restart) {
  if (Object.keys(edits).length) {
    const r = await act(() => api("/api/settings", { values: edits }));
    if (!r) return;
    toast(t("savedNext"));
  }
  if (restart && S && S.game.installed) await act(() => api("/api/server/restart", {}), t("stopping"));
  await loadSettings();
}

// ── players (ADMIN-1, user 2026-11-02: "an interface for the server admins with chat, ban list and so on") ──────────
// The server answers "admin players|bans|chat <id>" with one console line "[BAMP-HEADLESS] DATA <id> <json>"; every
// public chat line comes as "CHAT name: text". Whatever a player wrote is put in as text, never as HTML.
const ADM = { players: null, bans: [], chat: [], seq: 0, status: null, statusAt: 0 };
const one = v => String(v ?? "").replace(/[\r\n]+/g, " ").trim();
const quoted = id => id.includes('"') ? id : '"' + id + '"';
const ask = what => sendCommand("admin " + what + " m" + (++ADM.seq));
let admQueued = false;
function adminLine(l) {
  const m = /\[BAMP-HEADLESS\] ([A-Z]+) ?(.*)$/.exec(l.text || "");
  if (!m) return;
  const kind = m[1], rest = m[2];
  if (kind === "DATA") {
    let d = null;
    try { d = JSON.parse(rest.slice(rest.indexOf(" ") + 1)); } catch (e) { return; }
    if (Array.isArray(d.players)) ADM.players = d.players;
    if (Array.isArray(d.bans)) ADM.bans = d.bans;
    if (Array.isArray(d.chat)) ADM.chat = d.chat.map(c => ({ time: Date.parse(c.t) || l.time, who: c.who, text: c.text }));
    if (d.state !== undefined) { ADM.status = d; ADM.statusAt = l.time || Date.now(); }
  } else if (kind === "CHAT") {
    const i = rest.indexOf(": ");
    if (i > 0) { ADM.chat.push({ time: l.time, who: rest.slice(0, i), text: rest.slice(i + 2) }); if (ADM.chat.length > 100) ADM.chat.shift(); }
  } else if (kind === "JOIN" || kind === "LEAVE" || kind === "OUT") {
    // only what happens now - not the lines a new stream replays from the buffer
    const live = current === "players" && Date.now() - (l.time || 0) < 15000;
    if (live && kind !== "OUT") ask("players");
    if (live && kind === "OUT" && /^OK (un)?banned/.test(rest)) ask("bans");
    if (live && kind === "OUT" && /^OK (kicked|banned)/.test(rest)) ask("players");
    if (live && kind === "OUT" && /^OK restart/.test(rest)) ask("status");
    return;
  } else return;
  if (current === "players" && !admQueued) { admQueued = true; requestAnimationFrame(() => { admQueued = false; renderAdmin(); }); }
}
function restartLeft() {
  const st = ADM.status;
  const left = st && st.restartIn >= 0 ? st.restartIn - Math.floor((Date.now() - ADM.statusAt) / 1000) : -1;
  $("#restartLeft").textContent = left >= 0 ? t(st.countdown === "stop" ? "stopLeft" : "restartLeft", left >= 60 ? Math.round(left / 60) + " min" : Math.max(0, left) + " s") : "";
  $("#bRestartOff").hidden = !(left >= 0) || st.countdown === "stop";
}
setInterval(() => { if (ADM.status && ADM.status.restartIn >= 0 && current === "players") restartLeft(); }, 1000);
function renderAdmin() {
  restartLeft();
  const on = $("#pOnline");
  on.textContent = "";
  const ps = ADM.players || [];
  if (!ps.length) on.append(el("tr", {}, el("td", { class: "muted", text: t("nobody") })));
  for (const p of ps)
    on.append(el("tr", {}, el("td", { text: p.name }), el("td", { class: "num", text: p.ping >= 0 ? t("ping", p.ping) : "-" }), el("td", { text: t("onFor", fmtSpan(p.secs) === "-" ? t("mins", 0) : fmtSpan(p.secs)) }),
      el("td", {}, el("div", { class: "row" },
        el("button", { class: "btn small", text: t("msg"), onclick: () => { const v = prompt(t("msgTo", p.name), ""); if (v && one(v)) sendCommand("msg " + quoted(p.id) + " " + one(v)); } }),
        el("button", { class: "btn small", text: t("kick"), onclick: () => { const v = prompt(t("reasonKick", p.name), ""); if (v !== null) sendCommand("kick " + quoted(p.id) + " " + one(v)); } }),
        el("button", { class: "btn small danger", text: t("ban"), onclick: () => { const v = prompt(t("reasonBan", p.name), ""); if (v !== null) sendCommand("ban " + quoted(p.id) + " " + one(v)); } })))));
  const to = $("#chatTo"), keep = to.value;
  to.textContent = "";
  to.append(el("option", { value: "", text: t("toAll") }));
  for (const p of ps) to.append(el("option", { value: p.id, text: p.name }));
  to.value = [...to.options].some(o => o.value === keep) ? keep : "";
  const log = $("#chatLog"), atEnd = log.scrollHeight - log.scrollTop - log.clientHeight < 40;
  log.textContent = "";
  if (!ADM.chat.length) log.append(el("span", { class: "l muted", text: t("chatNone") }));
  for (const c of ADM.chat) log.append(el("span", { class: "l" }, el("span", { class: "t", text: fmtClock(c.time) }), el("span", { class: "who", text: c.who + ":" }), c.text));
  if (atEnd) log.scrollTop = log.scrollHeight;
  const bt = $("#pBans");
  bt.textContent = "";
  if (!ADM.bans.length) bt.append(el("tr", {}, el("td", { class: "muted", colspan: "5", text: t("bansNone") })));
  for (const b of ADM.bans)
    bt.append(el("tr", {}, el("td", { text: b.name || "?" }),
      el("td", { text: (b.utc ? new Date(b.utc).toLocaleDateString(lang === "de" ? "de-DE" : "en-GB") : "-") + (b.day ? " · " + t("day", b.day) : "") }),
      el("td", { text: b.reason || "-" }), el("td", { text: b.how === "Steam account" ? t("howSteam") : t("howIp") }),
      el("td", {}, el("div", { class: "row" }, el("button", { class: "btn small", text: t("unban"), onclick: () => { if (confirm(t("confirmUnban", b.name || b.key))) sendCommand("unban " + b.key); } })))));
  $("#bUnbanAll").hidden = ADM.bans.length < 2;
}
async function loadPlayers() {
  const r = await act(() => api("/api/players"));
  if (!r) return;
  if (!ADM.players) ADM.players = (r.online || []).map(n => ({ id: n, name: n, ping: -1, secs: 0 }));   // until the server answers
  renderAdmin();
  if (S && S.game && S.game.state === "running") { ask("players"); ask("bans"); ask("chat"); ask("status"); }
  const all = $("#pAll");
  all.textContent = "";
  for (const p of r.all.sort((a, b) => b.hours - a.hours))
    all.append(el("tr", {}, el("td", { text: p.name }), el("td", { class: "num", text: fmtNum(p.hours, 1) }), el("td", { class: "num", text: fmtNum(p.sessions) }),
      el("td", { text: fmtDate(p.firstSeen) }), el("td", { text: fmtDate(p.lastSeen) })));
}

// ── backups ──────────────────────────────────────────────────────────────────────────────────────────────────────────
async function loadBackups() {
  const list = await act(() => api("/api/backups"));
  if (!list) return;
  const tb = $("#bList");
  tb.textContent = "";
  if (!list.length) tb.append(el("tr", {}, el("td", { class: "muted", colspan: "4", text: t("noBackups") })));
  for (const b of list)
    tb.append(el("tr", {}, el("td", { text: fmtDate(b.time) }), el("td", { class: "mono small", text: b.name }), el("td", { class: "num", text: fmtNum(b.size / 1048576, 1) + " MB" }),
      el("td", {}, el("div", { class: "row" },
        el("a", { class: "btn small", href: "/api/backups/file/" + encodeURIComponent(b.name), download: b.name, text: t("download") }),
        el("button", { class: "btn small", text: t("restore"), onclick: () => startJob("/api/backups/restore", { name: b.name }, t("confirmRestore", b.name)) }),
        el("button", { class: "btn small danger", text: t("del"), onclick: () => { if (confirm(t("confirmDelete", b.name))) act(() => api("/api/backups/delete", { name: b.name })).then(loadBackups); } })))));
  const sel = $("#bHour");
  if (!sel.options.length) {
    sel.append(el("option", { value: "-1", text: t("hourOff") }));
    for (let h = 0; h < 24; h++) sel.append(el("option", { value: String(h), text: String(h).padStart(2, "0") + ":00" }));
  }
  if (S) { sel.value = String(S.backups.hour); $("#bKeep").value = S.backups.keep; }
}

// ── account / manager ────────────────────────────────────────────────────────────────────────────────────────────────
async function loadManager() {
  const c = await act(() => api("/api/manager"));
  if (!c) return;
  $("#mPort").value = c.WebPort;
  $("#mBind").value = c.WebBind;
  $("#mAuto").checked = !!c.AutoStart;
  $("#mLang").value = c.Language || "";
  const links = $("#logLinks");
  if (!links.childNodes.length)
    for (const [k, label] of [["console", "logConsole"], ["manager", "logManager"], ["events", "logEvents"], ["preloader", "logPreloader"]])
      links.append(el("a", { class: "btn small", href: "/api/logs/" + k, download: "", "data-t": label, text: t(label) }));
}

// ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────────────────
function wire() {
  $$("[data-lang]").forEach(b => b.addEventListener("click", switchLang));
  $$("#tabs button").forEach(b => b.addEventListener("click", () => go(b.dataset.tab)));
  $$("[data-goto]").forEach(b => b.addEventListener("click", () => go(b.dataset.goto)));

  $("#loginForm").addEventListener("submit", async ev => {
    ev.preventDefault();
    $("#lErr").textContent = "";
    try {
      const r = await api("/api/login", { user: $("#lUser").value.trim(), password: $("#lPass").value });
      $("#lPass").value = "";
      await showApp(r.user);
    } catch (e) { $("#lErr").textContent = e.message; }
  });
  $("#logout").addEventListener("click", async () => { await act(() => api("/api/logout", {})); showLogin(); });

  $("#bStart").addEventListener("click", () => act(() => api("/api/server/start", {}), t("started")).then(refresh));
  $("#bStop").addEventListener("click", () => { if (confirm(t("confirmStop"))) act(() => api("/api/server/stop", {}), t("stopping")).then(refresh); });
  $("#bRestart").addEventListener("click", () => act(() => api("/api/server/restart", {}), t("stopping")).then(refresh));
  $("#bSave").addEventListener("click", () => act(() => api("/api/server/command", { line: "save" }), t("saveSent")));
  $("#bKill").addEventListener("click", () => { if (confirm(t("confirmKill"))) act(() => api("/api/server/kill", {})).then(refresh); });
  $("#copyKey").addEventListener("click", async () => {
    try { await navigator.clipboard.writeText(S.key); toast(t("copied")); }
    catch (e) { const r = document.createRange(); r.selectNodeContents($("#vKey")); const s = getSelection(); s.removeAllRanges(); s.addRange(r); }
  });

  for (const id of ["#fGame", "#fManager", "#fJob"]) $(id).addEventListener("change", () => renderConsole(true));
  const hist = [];
  let histPos = 0;
  $("#cmdForm").addEventListener("submit", ev => {
    ev.preventDefault();
    const v = $("#cmdIn").value;
    if (!v.trim()) return;
    hist.push(v);
    histPos = hist.length;
    $("#cmdIn").value = "";
    sendCommand(v);
  });
  $("#cmdIn").addEventListener("keydown", ev => {
    if (ev.key === "ArrowUp" && histPos > 0) { $("#cmdIn").value = hist[--histPos]; ev.preventDefault(); }
    else if (ev.key === "ArrowDown") { histPos = Math.min(hist.length, histPos + 1); $("#cmdIn").value = hist[histPos] || ""; ev.preventDefault(); }
  });
  for (const c of ["status", "players", "save", "saves", "banlist"])
    $("#chips").append(el("button", { type: "button", class: "btn small mono", text: c, onclick: () => sendCommand(c) }));

  $("#setSave").addEventListener("click", () => saveSettings(false));
  $("#setSaveRestart").addEventListener("click", () => saveSettings(true));
  $("#setReset").addEventListener("click", () => { edits = {}; renderSettings(); });
  window.addEventListener("beforeunload", ev => { if (Object.keys(edits).length) { ev.preventDefault(); ev.returnValue = ""; } });

  $("#chatForm").addEventListener("submit", ev => {
    ev.preventDefault();
    const v = one($("#chatIn").value), to = $("#chatTo").value;
    if (v) sendCommand(to ? "msg " + quoted(to) + " " + v : "say " + v).then(() => { $("#chatIn").value = ""; });
  });
  $("#banForm").addEventListener("submit", ev => {
    ev.preventDefault();
    const v = one($("#banIn").value);
    if (!v) return;
    const r = prompt(t("reasonBan", v), "");
    if (r !== null) sendCommand("ban " + quoted(v) + " " + one(r)).then(() => { $("#banIn").value = ""; ask("bans"); });
  });
  $("#bUnbanName").addEventListener("click", () => { const v = one($("#banIn").value); if (v) sendCommand("unban " + v).then(() => { $("#banIn").value = ""; ask("bans"); }); });
  $("#bUnbanAll").addEventListener("click", () => { if (confirm(t("confirmUnbanAll"))) sendCommand("unban all").then(() => ask("bans")); });
  $("#noticeForm").addEventListener("submit", ev => { ev.preventDefault(); const v = one($("#noticeIn").value); if (v) sendCommand("notice " + v).then(() => { $("#noticeIn").value = ""; }); });
  $("#bRestartIn").addEventListener("click", () => {
    const v = prompt(t("restartMin"), "5");
    if (v === null) return;
    const m = Number(String(v).replace(",", "."));
    if (m > 0) sendCommand("restart " + m);
  });
  $("#bRestartOff").addEventListener("click", () => sendCommand("restart cancel"));

  $("#gameForm").addEventListener("submit", ev => {
    ev.preventDefault();
    const body = { user: $("#gUser").value.trim(), password: $("#gPass").value, code: $("#gCode").value.trim() };
    $("#gPass").value = "";
    $("#gCode").value = "";
    startJob("/api/install/game", body);
  });
  $("#protonForm").addEventListener("submit", ev => { ev.preventDefault(); startJob("/api/install/proton", { version: $("#pVersion").value.trim() }); });
  $("#bUpdateFiles").addEventListener("click", () => startJob("/api/install/files", {}));
  $("#bCheck").addEventListener("click", () => act(async () => { S = await api("/api/install/check", {}); renderStatus(); }));

  $("#bBackup").addEventListener("click", () => act(async () => { const j = await api("/api/backups", {}); renderJob(j); setTimeout(loadBackups, 25000); }, t("backupNow") + " ..."));
  $("#schedForm").addEventListener("submit", ev => {
    ev.preventDefault();
    act(() => api("/api/manager", { BackupHour: Number($("#bHour").value), BackupKeep: Number($("#bKeep").value) }), t("mgrSaved")).then(refresh);
  });

  $("#pwForm").addEventListener("submit", async ev => {
    ev.preventDefault();
    $("#pwErr").textContent = "";
    if ($("#pwNew").value !== $("#pwRep").value) { $("#pwErr").textContent = t("pwMismatch"); return; }
    try {
      await api("/api/account/password", { current: $("#pwCur").value, next: $("#pwNew").value });
      for (const id of ["#pwCur", "#pwNew", "#pwRep"]) $(id).value = "";
      toast(t("pwChanged"));
    } catch (e) { $("#pwErr").textContent = e.message; }
  });
  $("#mgrForm").addEventListener("submit", ev => {
    ev.preventDefault();
    act(async () => {
      const r = await api("/api/manager", { WebPort: Number($("#mPort").value), WebBind: $("#mBind").value.trim(), AutoStart: $("#mAuto").checked, Language: $("#mLang").value });
      toast(r.restartManager ? t("mgrSavedRestart") : t("mgrSaved"));
    });
  });

  document.addEventListener("visibilitychange", () => { if (!document.hidden && me) refresh(); });
  setInterval(() => { if (me && !document.hidden) refresh(); }, 2500);
}

async function boot() {
  applyTexts();
  wire();
  try {
    const s = await api("/api/session");
    let stored = null;
    try { stored = localStorage.getItem("gps-lang"); } catch (e) { }
    if (!stored && s && s.lang) { lang = s.lang; applyTexts(); }
    if (s && s.user) await showApp(s.user);
    else { $("#lUser").value = "admin"; showLogin(); }
  } catch (e) { showLogin(); }
}
boot();
