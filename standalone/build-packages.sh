#!/usr/bin/env bash
# STANDALONE-1: the Going Public server without AMP - three release files from this repository:
#   GoingPublicServer-<ver>-Windows.zip    GoingPublicServer.exe, Reset-Password.bat, LIESMICH.txt, payload/
#   GoingPublicServer-<ver>-Linux.tar.gz   GoingPublicServer, install.sh, LIESMICH.txt, payload/
#   GoingPublicServer-<ver>-Docker.zip     Dockerfile, docker-compose.yml, entrypoint.sh, LIESMICH.md
# payload/ holds the server files of the server zip (make-server-release.sh): 1331550/ (the server-only preloader),
# bamp-mod/GoingPublicServer/, bamp-tools/ (description of the settings + translations), telemetry-data/web/ (ranking
# page), and VERSION ("<version> <build>", the build = the player zip's hash: the Workshop file that fits).
#
#   standalone/build-packages.sh [--build] <out-dir>
#       --build   build the mod and the preloader first (make-server-release.sh --build); the manager is always built.
#                 Without it the last builds are used - so the server zip and the Workshop zip stay the same build.
set -euo pipefail
REPO="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
BUILD=()
[ "${1:-}" = "--build" ] && { BUILD=(--build); shift; }
OUT="${1:?Zielordner angeben}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

echo "== Server-Dateien (make-server-release.sh) =="
"$REPO/headless/amp/make-server-release.sh" "${BUILD[@]}" "$STAGE/rel" >/dev/null
SZIP="$STAGE/rel/GoingPublicServer-Server.zip"
mkdir -p "$STAGE/payload"
python3 - "$SZIP" "$STAGE/payload" <<'PY'
import sys, zipfile, pathlib
src, out = sys.argv[1], pathlib.Path(sys.argv[2])
keep = ("1331550/", "bamp-mod/", "telemetry-data/web/", "bamp-tools/bigambitionsconfig.json", "bamp-tools/i18n/")
with zipfile.ZipFile(src) as z:
    for n in z.namelist():
        if n.endswith("/") or not n.startswith(keep):
            continue
        dst = out / n
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(z.read(n))
PY
VER="$(sed -n 's/.*PLUGIN_VERSION *= *"\([^"]*\)".*/\1/p' "$REPO/src/MyPluginInfo.cs" | head -n1)"
HASH="$(python3 -c 'import hashlib,sys; print(hashlib.sha256(open(sys.argv[1],"rb").read()).hexdigest()[:8])' "$STAGE/payload/bamp-mod/GoingPublicServer/BigAmbitionsMP.dll")"
printf '%s %s\n' "$VER" "$HASH" > "$STAGE/payload/VERSION"
[ -f "$STAGE/payload/1331550/winhttp.dll" ] && [ -f "$STAGE/payload/bamp-mod/GoingPublicServer/BigAmbitionsMP.dll" ] || { echo "payload unvollständig" >&2; exit 1; }
echo "   Version $VER, Build $HASH (Workshop-Datei GoingPublicServer-$VER-$HASH.zip)"

echo "== Verwaltung (dotnet publish) =="
for rid in linux-x64 win-x64; do
    dotnet publish "$REPO/standalone/manager/GoingPublicServer.Manager.csproj" -c Release -r "$rid" -nologo -o "$STAGE/mgr-$rid" >/dev/null
done

echo "== Pakete =="
rm -f "$OUT"/GoingPublicServer-"$VER"-{Windows.zip,Linux.tar.gz,Docker.zip}

W="$STAGE/win/GoingPublicServer"
mkdir -p "$W"
cp "$STAGE/mgr-win-x64/GoingPublicServer.exe" "$W/"
cp -r "$STAGE/payload" "$W/payload"
cp "$REPO/standalone/windows/Reset-Password.bat" "$REPO/standalone/windows/LIESMICH.txt" "$W/"
sed -i 's/$/\r/' "$W/Reset-Password.bat" "$W/LIESMICH.txt"   # Windows line ends
(cd "$STAGE/win" && python3 - "$OUT/GoingPublicServer-$VER-Windows.zip" GoingPublicServer <<'PY'
import os, sys, zipfile
out, root = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for base, dirs, files in os.walk(root):
        dirs.sort()
        for f in sorted(files):
            p = os.path.join(base, f)
            z.write(p, p.replace(os.sep, "/"))
PY
)

L="$STAGE/linux/GoingPublicServer-$VER-Linux"
mkdir -p "$L"
install -m 755 "$STAGE/mgr-linux-x64/GoingPublicServer" "$L/GoingPublicServer"
install -m 755 "$REPO/standalone/linux/install.sh" "$L/install.sh"
install -m 644 "$REPO/standalone/linux/LIESMICH.txt" "$L/"
cp -r "$STAGE/payload" "$L/payload"
tar -czf "$OUT/GoingPublicServer-$VER-Linux.tar.gz" -C "$STAGE/linux" "GoingPublicServer-$VER-Linux"

D="$STAGE/docker/GoingPublicServer-$VER-Docker"
mkdir -p "$D"
for f in Dockerfile docker-compose.yml entrypoint.sh LIESMICH.md; do cp "$REPO/standalone/docker/$f" "$D/"; done
sed -i "s/^ARG GPS_VERSION=.*/ARG GPS_VERSION=$VER/" "$D/Dockerfile"
sed -i "s/GPS_VERSION: \".*\"/GPS_VERSION: \"$VER\"/; s#image: goingpublicserver:.*#image: goingpublicserver:$VER#" "$D/docker-compose.yml"
(cd "$STAGE/docker" && python3 - "$OUT/GoingPublicServer-$VER-Docker.zip" "GoingPublicServer-$VER-Docker" <<'PY'
import os, sys, zipfile
out, root = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for f in sorted(os.listdir(root)):
        info = zipfile.ZipInfo(root + "/" + f)
        info.external_attr = (0o755 if f.endswith(".sh") else 0o644) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, open(os.path.join(root, f), "rb").read())
PY
)
cp "$SZIP" "$OUT/GoingPublicServer-Server.zip"
cp "$STAGE/rel/GoingPublicServer-$VER.zip" "$OUT/GoingPublicServer-$VER-$HASH.zip"   # the player zip (Steam Workshop)
echo "Release $VER (Build $HASH):"
ls -l "$OUT"/GoingPublicServer-*
