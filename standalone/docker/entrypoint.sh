#!/bin/sh
# Going Public Server in Docker: /data belongs to the server user (PUID/PGID, default 1000), the manager runs as it.
# Its first start shows the access data of the web interface here, in the container's output.
set -e
PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
if [ "$(id -u)" = 0 ]; then
    mkdir -p /data
    [ "$(stat -c %u /data)" = "$PUID" ] || chown -R "$PUID:$PGID" /data
    exec setpriv --reuid="$PUID" --regid="$PGID" --clear-groups env HOME=/data /opt/goingpublicserver/GoingPublicServer run --data /data "$@"
fi
exec /opt/goingpublicserver/GoingPublicServer run --data /data "$@"
