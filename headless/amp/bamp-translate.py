#!/usr/bin/env python3
"""CHAT-TRANSLATE-1: the players' chat in their own language - German <-> English, translated on this server only.

The user 2026-11-08: "für den Chat eine Live-Übersetzung für Spieler ... ich schreibe in Deutsch und der andere erhält
es in Englisch, er schreibt in Englisch, ich erhalte es in Deutsch". Chosen: the server translates (no account, no
service on the internet - the chat never leaves it), German and English, on for all and switchable.

bamp-prestart.sh runs both steps in the background (the game does not wait for them; PRESTART-1, the AMP template's
pre-start stage, and bamp-start.sh of older templates):
  python3 -I bamp-translate.py setup DIR
      once: DIR/venv with CTranslate2 + SentencePiece (from PyPI) and DIR/models with the two Argos Translate models
      (argos-net.com, checked by SHA-256) - about 550 MB. Nothing outside DIR changes. DIR is bigambitions/bamp-translate,
      outside the game folder 1331550 that AMP backs up.
  DIR/venv/bin/python -I bamp-translate.py serve DIR STATEFILE PARENTPID
      loads the models, answers on 127.0.0.1 only (a free port, a random token) and writes port and token into
      STATEFILE (headless/translate.json), which the mod reads. Ends with the game and removes STATEFILE: PARENTPID gone,
      or with PARENTPID 0 the game's status file (headless/status.json) says so - see game_gone().

  POST /translate (header X-Token)  {"text": "...", "hint": "de", "targets": ["en"]}
       -> {"src": "de", "out": {"en": "..."}}   src "" = no language found or nothing worth translating;
                                                 out holds only translations that differ from the text
  GET  /health    (header X-Token)  -> {"ok": true, "langs": ["de", "en"]}

The mod's side is src/ChatTranslate.cs. Tested 2026-11-08: 0.1 s per chat line on two threads, 375 MB of memory.
"""
import hashlib
import hmac
import http.server
import json
import os
import re
import secrets
import shutil
import signal
import subprocess
import sys
import threading
import time
import urllib.request
import zipfile
from collections import OrderedDict

SETUP_VERSION = 1
PACKAGES = ["ctranslate2==4.8.5", "sentencepiece==0.2.3"]
PACKAGES_FALLBACK = ["ctranslate2>=4,<5", "sentencepiece>=0.2,<0.3"]   # a Python without wheels of the pinned versions
GET_PIP = "https://bootstrap.pypa.io/get-pip.py"
# (pair, url, sha256, bytes): Argos Translate 1.3, German <-> English (CTranslate2 model + SentencePiece)
MODELS = [
    ("de_en", "https://argos-net.com/v1/translate-de_en-1_3.argosmodel",
     "becc2b0011f8249fcb89be9ecb75ba0d876b1fab93c28ee6ff0420936897d637", 150512831),
    ("en_de", "https://argos-net.com/v1/translate-en_de-1_3.argosmodel",
     "6cd847f0c06c9c66013e6b0932e07fd54a6d90894659c02bf6c5247b72fb25b1", 150508297),
]
LANGS = ["de", "en"]


def say(msg):
    print("[BAMP-Start] Chat-Übersetzung: " + msg, flush=True)


# ── setup ────────────────────────────────────────────────────────────────────────────────────────────────────────────

def model_dir(d, pair):
    return os.path.join(d, "models", "translate-%s-1_3" % pair)


def model_ok(path):
    return os.path.isfile(os.path.join(path, "model", "model.bin")) and os.path.isfile(os.path.join(path, "sentencepiece.model"))


def venv_python(d):
    return os.path.join(d, "venv", "bin", "python")


def last_line(r):
    lines = [l for l in ((r.stderr or "") + "\n" + (r.stdout or "")).splitlines() if l.strip()]
    return lines[-1][:200] if lines else "exit %s" % r.returncode


def run_ok(cmd, **kw):
    try:
        return subprocess.run(cmd, capture_output=True, text=True, timeout=120, **kw).returncode == 0
    except Exception:
        return False


def fetch(url, dest, sha=None, size=None):
    part = dest + ".part"
    h = hashlib.sha256()
    n = 0
    req = urllib.request.Request(url, headers={"User-Agent": "bamp-translate/1"})
    with urllib.request.urlopen(req, timeout=60) as r, open(part, "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            n += len(b)
            if size and n > size:
                raise RuntimeError("Download größer als erwartet: " + os.path.basename(dest))
            f.write(b)
            h.update(b)
    if (sha and h.hexdigest() != sha) or (size and n != size):
        os.remove(part)
        raise RuntimeError("Prüfsumme stimmt nicht: " + os.path.basename(dest))
    os.replace(part, dest)


def ensure_venv(d):
    venv = os.path.join(d, "venv")
    py = venv_python(d)
    if os.path.isfile(py) and run_ok([py, "-I", "-m", "pip", "--version"]):
        return py
    r = subprocess.run([sys.executable, "-m", "venv", "--clear", venv], capture_output=True, text=True)
    if r.returncode == 0 and run_ok([py, "-I", "-m", "pip", "--version"]):
        return py
    # no ensurepip (Debian/Ubuntu without python3-venv): an empty venv, then pip from its official bootstrap
    r = subprocess.run([sys.executable, "-m", "venv", "--clear", "--without-pip", venv], capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError("venv: " + last_line(r))
    dl = os.path.join(d, "dl")
    os.makedirs(dl, exist_ok=True)
    gp = os.path.join(dl, "get-pip.py")
    fetch(GET_PIP, gp)
    r = subprocess.run([py, "-I", gp, "--quiet", "--no-cache-dir", "--disable-pip-version-check", "--no-warn-script-location"],
                       cwd=dl, capture_output=True, text=True, timeout=900)
    if r.returncode != 0:
        raise RuntimeError("pip: " + last_line(r))
    return py


def pip_install(d, py):
    # no cache: the game's HOME is the game folder 1331550, which AMP backs up
    base = [py, "-I", "-m", "pip", "install", "--quiet", "--no-cache-dir", "--disable-pip-version-check", "--no-input",
            "--only-binary=:all:"]
    r = subprocess.run(base + PACKAGES, cwd=d, capture_output=True, text=True, timeout=1800)
    if r.returncode != 0:
        r = subprocess.run(base + PACKAGES_FALLBACK, cwd=d, capture_output=True, text=True, timeout=1800)
        if r.returncode != 0:
            raise RuntimeError("Pakete: " + last_line(r))


def ensure_model(d, pair, url, sha, size):
    target = model_dir(d, pair)
    if model_ok(target):
        return
    name = os.path.basename(target)
    dl = os.path.join(d, "dl")
    os.makedirs(dl, exist_ok=True)
    arc = os.path.join(dl, name + ".argosmodel")
    say("lade das Sprachmodell %s (150 MB) ..." % pair.replace("_", " -> "))
    fetch(url, arc, sha, size)
    tmp = os.path.join(d, "models", ".tmp-" + name)
    shutil.rmtree(tmp, ignore_errors=True)
    with zipfile.ZipFile(arc) as z:
        for i in z.infolist():
            p = i.filename.replace("\\", "/")
            if p.startswith("/") or ".." in p.split("/"):
                raise RuntimeError("unsicherer Pfad im Modell: " + p[:80])
        z.extractall(tmp)
    src = os.path.join(tmp, name)
    if not model_ok(src):
        raise RuntimeError("Modell unvollständig: " + name)
    shutil.rmtree(target, ignore_errors=True)
    os.replace(src, target)
    shutil.rmtree(tmp, ignore_errors=True)
    os.remove(arc)


def setup(d):
    os.makedirs(d, exist_ok=True)
    mark = os.path.join(d, "ready.json")
    try:
        with open(mark, encoding="utf-8") as f:
            if json.load(f).get("v") == SETUP_VERSION and os.path.isfile(venv_python(d)) \
                    and all(model_ok(model_dir(d, m[0])) for m in MODELS):
                return 0
    except Exception:
        pass
    t0 = time.time()
    say("erste Einrichtung - lädt einmalig etwa 400 MB (zwei Sprachmodelle und Python-Pakete); das Spiel startet schon.")
    try:
        py = ensure_venv(d)
        pip_install(d, py)
        for m in MODELS:
            ensure_model(d, *m)
        r = subprocess.run([py, "-I", "-c", "import ctranslate2, sentencepiece"], capture_output=True, text=True, timeout=300)
        if r.returncode != 0:
            raise RuntimeError("die Pakete laden nicht: " + last_line(r))
        with open(mark + ".tmp", "w", encoding="utf-8") as f:
            json.dump({"v": SETUP_VERSION, "packages": PACKAGES, "models": [m[0] for m in MODELS],
                       "python": sys.version.split()[0], "at": int(time.time())}, f)
        os.replace(mark + ".tmp", mark)
        shutil.rmtree(os.path.join(d, "dl"), ignore_errors=True)   # only this script's own downloads
        say("eingerichtet in %d s." % (time.time() - t0))
        return 0
    except Exception as e:
        say("Einrichtung fehlgeschlagen (%s) - der Chat geht unübersetzt raus; beim nächsten Start versucht es der Server "
            "erneut." % str(e)[:300])
        return 1


# ── language ─────────────────────────────────────────────────────────────────────────────────────────────────────────

# Words that tell German from English in chat lines. Words both languages use ("was", "will", "also", "in", "so", "man")
# are in neither list; UNIVERSAL lines ("lol", "gg wp", "ok") are not translated at all.
DE = set("""
ich du er wir ihr und oder aber nicht nichts kein keine keinen ist bin bist sind seid war waren hab habe hast hat haben
hatte hatten wird werden wurde würde würden kann kannst können konnte muss musst müssen soll sollst sollen darf willst
möchte möchtest mag magst der den dem des das die ein eine einen einem einer eines mein meine meinen meinem dein deine
deinen deinem sein seine seinen unser unsere euer eure wie wo wer wen wem warum wieso weshalb wann welche welcher welches
auch noch schon mal jetzt gleich heute morgen gestern hier dort da ja nein nee doch danke bitte gut sehr viel viele mehr
nur immer wieder mit für auf aus bei nach von vom zum zur zu im ins über unter gegen ohne weil wenn dann dass ob mich dich
mir dir uns euch ihm ihn ihnen sich alles etwas geht gehts läuft machen mache machst macht gibt gibts kommt komme kommst
brauche brauchst braucht hallo moin servus tschüss genau stimmt eben halt einfach wirklich vielleicht leider geil krass
bisschen gerade grad warte schau guck sag sagen weiß weißt glaube denke meinst kaufen verkaufen laden geld firma haus
spiel habs isses nen ne nix nö wat klar wieviel
""".split())
EN = set("""
i you he she we they and or but not nothing is am are were be been being have has had having would can could should
must may might do does did done the a an my your his her its our their this that these those how where who whom why when
what which too still already now today tomorrow yesterday here there yes yeah yep nope thanks thank please good very much
many more only always again with for on of from at to into about under against without because if then than me him them
us it it's i'm dont don't doesn't didn't can't cant won't wont isn't aren't wasn't i'll ill i've ive you're youre
gonna wanna gotta just really maybe money company car house game buy sell store shop hello bye see get got go going come
coming need want know think say said look wait let lets let's guys mate dude something anything everything who's what's
whats there's theres where's wheres how's hows lend pay paid back
""".split())
UNIVERSAL = set("""
ok okay k kk gg ggs wp gl hf lol lmao rofl xd haha hehe hihi hi hey yo jo brb afk gn thx ty np omg wtf nice cool top sorry
sry plz pls mhm hm hmm ah oh ahh ohh wow yay ups oops
""".split())
WORD = re.compile(r"[a-zäöüß']+")
URL = re.compile(r"(?i)\b(?:https?://|www\.)")


def detect(text, hint):
    t = text.lower()
    words = [w.strip("'") for w in WORD.findall(t)]
    content = [w for w in words if w and w not in UNIVERSAL]
    if not content:
        return ""
    de = sum(1 for w in content if w in DE) + (2 if re.search(r"[äöüß]", t) else 0)
    en = sum(1 for w in content if w in EN)
    if de > en:
        return "de"
    if en > de:
        return "en"
    return hint if hint in LANGS else ""


def norm(s):
    return re.sub(r"\W+", "", s.lower())


# ── the game's words and "du" ────────────────────────────────────────────────────────────────────────────────────────

# Terms the models do not know, swapped before translating: given the other language's word, the model keeps it and
# fits article and grammar around it (tested 2026-11-08: "my headend has 1500 connections" -> "meine Kopfstelle hat
# jetzt 1500 Anschlüsse"; the model alone said "head site", "head office", "Headend").
BEFORE = {
    "de_en": [(re.compile(r"(?i)\bkopfstellen\b"), "headends"), (re.compile(r"(?i)\bkopfstelle\b"), "headend")],
    "en_de": [(re.compile(r"(?i)\bheadends\b"), "Kopfstellen"), (re.compile(r"(?i)\bheadend\b"), "Kopfstelle")],
}
AFTER = {
    "en_de": [(re.compile(r"\bsich verschmelzen\b"), "fusionieren"), (re.compile(r"\bverschmelzen\b"), "fusionieren")],
}

# English -> German: the model mostly says "Sie", players say "du" to each other. A capital "Sie" after the verb, and
# "Ihr..."/"Ihnen" in the middle of a sentence, are always the polite "you" ("they" is a small "sie" there); a "Sie" at
# the start of a sentence can be "they", so that one changes only when the English line says "you" and not "they".
DU = {
    "möchten": "möchtest", "können": "kannst", "könnten": "könntest", "haben": "hast", "hätten": "hättest",
    "hatten": "hattest", "sind": "bist", "waren": "warst", "wären": "wärst", "wollen": "willst", "wollten": "wolltest",
    "werden": "wirst", "würden": "würdest", "wurden": "wurdest", "wissen": "weißt", "müssen": "musst",
    "mussten": "musstest", "sollten": "solltest", "sollen": "sollst", "dürfen": "darfst", "brauchen": "brauchst",
    "sehen": "siehst", "kommen": "kommst", "gehen": "gehst", "spielen": "spielst", "machen": "machst",
    "kaufen": "kaufst", "verkaufen": "verkaufst", "nehmen": "nimmst", "geben": "gibst", "finden": "findest",
    "denken": "denkst", "glauben": "glaubst", "meinen": "meinst", "suchen": "suchst", "zahlen": "zahlst",
    "bezahlen": "bezahlst", "leihen": "leihst", "warten": "wartest", "helfen": "hilfst", "fahren": "fährst",
    "arbeiten": "arbeitest", "wohnen": "wohnst", "heißen": "heißt", "mögen": "magst", "lesen": "liest",
    "sprechen": "sprichst", "verstehen": "verstehst", "bekommen": "bekommst", "planen": "planst", "bauen": "baust",
    "eröffnen": "eröffnest", "gründen": "gründest", "stellen": "stellst", "spielten": "spieltest", "kauften": "kauftest",
}
W_WORDS = r"(?:[Ww]ie viel|[Ww]ieviel|[Ww]ie lange|[Ww]ie|[Ww]o|[Ww]ohin|[Ww]oher|[Ww]as|[Ww]ann|[Ww]arum|[Ww]ieso|" \
          r"[Ww]eshalb|[Ww]elche[nrms]?|[Ww]en|[Ww]em|[Dd]ann|[Jj]etzt|[Hh]eute|[Mm]orgen|[Dd]enn)"
RE_YOU = re.compile(r"(?i)\b(you|your|yours|yourself|u|ya)\b")
RE_THEY = re.compile(r"(?i)\b(they|them|their|theirs)\b")
RE_VERB_SIE = re.compile(r"(^|[.!?]\s+|\b" + W_WORDS + r" )(\w+) Sie\b")
RE_LEAD_SIE = re.compile(r"(^|[.!?]\s+)Sie (\w+)\b")


def du_form(verb):
    d = DU.get(verb.lower())
    if not d:
        return None
    return d[0].upper() + d[1:] if verb[:1].isupper() else d


def informal(de, en):
    if not RE_YOU.search(en):
        return de

    def verb_sie(m):   # "Möchten Sie ..." / "wo haben Sie ..." -> "Möchtest du ..." / "wo hast du ..."
        d = du_form(m.group(2))
        return m.group(1) + d + " du" if d else m.group(0)

    de = RE_VERB_SIE.sub(verb_sie, de)
    if not RE_THEY.search(en):
        def lead(m):   # "Sie sollten ..." -> "Du solltest ..."
            d = du_form(m.group(2))
            return m.group(1) + "Du " + d if d else m.group(0)
        de = RE_LEAD_SIE.sub(lead, de)
    de = re.sub(r"\b(du|Du) sich\b", r"\1 dich", de)
    de = re.sub(r"\b(für|ohne|gegen|um) Sie\b", r"\1 dich", de)
    de = re.sub(r"(?<=[\w,] )Ihnen\b", "dir", de)
    de = re.sub(r"(?<=[\w,] )Ihr(e|en|em|er|es)?\b", lambda m: "dein" + (m.group(1) or ""), de)
    return de


# ── translator ───────────────────────────────────────────────────────────────────────────────────────────────────────

class Translator:
    def __init__(self, d, threads):
        import ctranslate2
        import sentencepiece
        self.models = {}
        for pair, _, _, _ in MODELS:
            path = model_dir(d, pair)
            if not model_ok(path):
                continue
            tr = ctranslate2.Translator(os.path.join(path, "model"), device="cpu", compute_type="int8",
                                        inter_threads=1, intra_threads=threads)
            sp = sentencepiece.SentencePieceProcessor(model_file=os.path.join(path, "sentencepiece.model"))
            self.models[pair] = (tr, sp)
        self.lock = threading.Lock()
        self.cache = OrderedDict()

    def translate(self, pair, text):
        key = (pair, text)
        with self.lock:
            if key in self.cache:
                self.cache.move_to_end(key)
                return self.cache[key]
            tr, sp = self.models[pair]
            src = text.strip()
            for rx, word in BEFORE.get(pair, []):
                src = rx.sub(word, src)
            parts = [s for s in re.split(r"(?<=[.!?])\s+", src) if s]
            res = tr.translate_batch([sp.encode(s, out_type=str) for s in parts], beam_size=4, max_decoding_length=256)
            out = " ".join(sp.decode(r.hypotheses[0]) for r in res)
            out = re.sub(r"\s*⁇\s*", " ", out).strip()   # what the model could not read (emoji)
            for rx, word in AFTER.get(pair, []):
                out = rx.sub(word, out)
            if pair == "en_de":
                out = informal(out, text)
            self.cache[key] = out
            if len(self.cache) > 1000:
                self.cache.popitem(last=False)
            return out

    def answer(self, text, hint, targets):
        text = text.strip()
        if not text or len(text) > 600 or URL.search(text):
            return {"src": "", "out": {}}
        src = detect(text, hint)
        out = {}
        if src:
            for t in targets:
                pair = "%s_%s" % (src, t)
                if t == src or pair not in self.models:
                    continue
                tr = self.translate(pair, text)
                if tr and norm(tr) != norm(text):
                    out[t] = tr
        return {"src": src, "out": out}


def alive(pid):
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def game_gone(status, since):
    """PRESTART-1: started from a pre-start stage there is no parent to watch, so the game's status file is watched (the
    mod rewrites it every 5 s): gone when it says "stopped", when this run's file is 5 minutes old (a crash or a hang),
    or when there is none of this run 15 minutes after the start (a first start can take that long)."""
    now = time.time()
    try:
        st = os.stat(status)
    except OSError:
        return now - since > 900
    if st.st_mtime < since - 5:   # the last run's file
        return now - since > 900
    if now - st.st_mtime > 300:
        return True
    try:
        with open(status, encoding="utf-8-sig") as f:
            return json.load(f).get("state") == "stopped"
    except Exception:
        return False


def serve(d, state, parent):
    t0 = time.time()
    try:
        tr = Translator(d, max(1, min(2, os.cpu_count() or 1)))
    except Exception as e:
        say("startet nicht (%s) - der Chat geht unübersetzt raus." % str(e)[:200])
        return 1
    if not tr.models:
        say("keine Sprachmodelle gefunden - der Chat geht unübersetzt raus.")
        return 1
    token = secrets.token_hex(16)

    class Handler(http.server.BaseHTTPRequestHandler):
        server_version = "bamp-translate"

        def log_message(self, *args):
            pass

        def _send(self, code, obj):
            body = json.dumps(obj, ensure_ascii=False).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _authorized(self):
            return hmac.compare_digest(self.headers.get("X-Token", ""), token)

        def do_GET(self):
            if not self._authorized():
                return self._send(403, {"error": "token"})
            if self.path == "/health":
                return self._send(200, {"ok": True, "langs": LANGS})
            self._send(404, {"error": "path"})

        def do_POST(self):
            if not self._authorized():
                return self._send(403, {"error": "token"})
            if self.path != "/translate":
                return self._send(404, {"error": "path"})
            try:
                n = int(self.headers.get("Content-Length") or 0)
                if n <= 0 or n > 16384:
                    return self._send(413, {"error": "size"})
                req = json.loads(self.rfile.read(n).decode("utf-8"))
                text = str(req.get("text") or "")
                hint = str(req.get("hint") or "")[:12].lower()
                targets = [str(x)[:12].lower() for x in (req.get("targets") or [])][:4]
                self._send(200, tr.answer(text, hint, targets))
            except Exception as e:
                self._send(400, {"error": str(e)[:200]})

    srv = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    srv.daemon_threads = True
    port = srv.server_address[1]
    tmp = state + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump({"port": port, "token": token, "pid": os.getpid(), "langs": LANGS, "started": int(time.time())}, f)
    os.chmod(tmp, 0o600)
    os.replace(tmp, state)

    def cleanup():
        try:
            with open(state, encoding="utf-8") as f:
                if json.load(f).get("token") == token:   # not a newer translator's file
                    os.remove(state)
        except Exception:
            pass

    def stop(*_):
        threading.Thread(target=srv.shutdown, daemon=True).start()

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)

    def watch():
        if parent > 0:
            while alive(parent):
                time.sleep(2)
        else:
            status, since = os.path.join(os.path.dirname(state), "status.json"), time.time()
            while not game_gone(status, since):
                time.sleep(5)
        stop()

    threading.Thread(target=watch, daemon=True).start()
    say("bereit - Deutsch <-> Englisch (Modelle in %.1f s geladen)." % (time.time() - t0))
    try:
        srv.serve_forever(poll_interval=1.0)
    finally:
        cleanup()
        say("beendet.")
    return 0


def main(argv):
    if len(argv) >= 3 and argv[1] == "setup":
        return setup(argv[2])
    if len(argv) >= 5 and argv[1] == "serve":
        return serve(argv[2], argv[3], int(argv[4]))
    if len(argv) >= 3 and argv[1] == "test":   # developer check: python bamp-translate.py test DIR "text" [hint]
        tr = Translator(argv[2], 2)
        print(json.dumps(tr.answer(argv[3] if len(argv) > 3 else "", argv[4] if len(argv) > 4 else "", LANGS), ensure_ascii=False))
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
