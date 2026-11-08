# Going Public Server in Docker - the Linux package, with everything Proton GE needs to run the Windows game.
#   docker compose up -d --build        (docker-compose.yml next to this file)
# The access data of the web interface appear once in the container's output:  docker compose logs goingpublicserver
# The program comes from GoingPublicServer-<version>-Linux.tar.gz next to this file, else from the GitHub release.
FROM debian:trixie-slim

ARG GPS_VERSION=1.0.16
ARG GPS_URL=https://github.com/amaus1209/BigAmbitionsGoingPublicServer/releases/download/v${GPS_VERSION}/GoingPublicServer-${GPS_VERSION}-Linux.tar.gz
ENV DEBIAN_FRONTEND=noninteractive LANG=C.UTF-8

RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl tar gzip xz-utils python3 procps tini util-linux xvfb xauth \
      libc6:i386 libstdc++6:i386 libgcc-s1:i386 \
      libfreetype6 libfreetype6:i386 libfontconfig1 libfontconfig1:i386 fonts-dejavu-core \
      libx11-6 libx11-6:i386 libxext6 libxrender1 libxcomposite1 libxcursor1 libxi6 libxrandr2 libxinerama1 libxkbcommon0 \
      libgl1 libgbm1 libvulkan1 libdbus-1-3 libudev1 libasound2t64 libpulse0 libgnutls30t64 libsdl2-2.0-0 \
 && rm -rf /var/lib/apt/lists/*

COPY Dockerfile GoingPublicServer-*-Linux.tar.g[z] /tmp/ctx/
RUN set -e; \
    f="$(ls /tmp/ctx/GoingPublicServer-*-Linux.tar.gz 2>/dev/null | head -n1 || true)"; \
    if [ -z "$f" ]; then curl -fsSL "$GPS_URL" -o /tmp/gps.tar.gz; f=/tmp/gps.tar.gz; fi; \
    mkdir -p /opt/goingpublicserver; \
    tar -xzf "$f" -C /opt/goingpublicserver --strip-components=1; \
    rm -rf /tmp/ctx /tmp/gps.tar.gz; \
    test -x /opt/goingpublicserver/GoingPublicServer

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
# "gps": the manager as the server user, e.g.  docker compose exec goingpublicserver gps reset-password
RUN chmod 755 /usr/local/bin/entrypoint.sh \
 && printf '%s\n' '#!/bin/sh' \
      'if [ "$(id -u)" = 0 ]; then exec setpriv --reuid="${PUID:-1000}" --regid="${PGID:-1000}" --clear-groups env HOME=/data /opt/goingpublicserver/GoingPublicServer --data /data "$@"; fi' \
      'exec /opt/goingpublicserver/GoingPublicServer --data /data "$@"' > /usr/local/bin/gps \
 && chmod 755 /usr/local/bin/gps

ENV GPS_DOCKER=1 GPS_WEB_PORT=8080 GPS_RESET_HINT="docker compose exec goingpublicserver gps reset-password"
VOLUME ["/data"]
EXPOSE 8080/tcp 7777/udp
STOPSIGNAL SIGTERM
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
