#!/usr/bin/env bash
#
# Sets up and releases Quillbridge on the VPS that already runs StockFlow. Run as root by the deploy
# workflow on every push, after it has uploaded the build to /srv/quillbridge-release; idempotent.
#
# Settings arrive on stdin as KEY=VALUE lines (never as arguments, which show in the process list):
#   DOMAIN          required — the hostname nginx serves the site on
#   JWT_KEY         required — at least 32 bytes
#   CLAUDE_API_KEY  optional — without it guides fall back to the template writer
#   CERT_EMAIL      optional — Let's Encrypt account e-mail
#
# What it shares with StockFlow, and nothing more: nginx, certbot, and the stockflow-mssql SQL Server
# container, in which it owns one database (`quillbridge`) through a login of its own.

set -euo pipefail

STAGE_DIR=/srv/quillbridge-release
APP_DIR=/opt/quillbridge/api
WEB_DIR=/var/www/quillbridge/web
CONF_DIR=/etc/quillbridge
APP_USER=quillbridge
API_PORT=5100
MSSQL_CONTAINER=stockflow-mssql
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

log() { echo "[quillbridge] $*"; }
die() { echo "[quillbridge] $*" >&2; exit 1; }

[[ "$(id -u)" -eq 0 ]] || die "must run as root"

# ─── Settings from stdin ─────────────────────────────────────────────────────────────────────────
DOMAIN="" JWT_KEY="" CLAUDE_API_KEY="" CERT_EMAIL=""
while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    case "$line" in
        DOMAIN=*)         DOMAIN="${line#*=}" ;;
        JWT_KEY=*)        JWT_KEY="${line#*=}" ;;
        CLAUDE_API_KEY=*) CLAUDE_API_KEY="${line#*=}" ;;
        CERT_EMAIL=*)     CERT_EMAIL="${line#*=}" ;;
    esac
done

[[ "$DOMAIN" =~ ^[A-Za-z0-9.-]+$ ]] || die "DOMAIN is missing or not a hostname: '$DOMAIN'"
[[ ${#JWT_KEY} -ge 32 ]] || die "JWT_KEY is missing or shorter than 32 characters"
# The values go into a systemd EnvironmentFile inside double quotes.
for value in "$JWT_KEY" "$CLAUDE_API_KEY"; do
    [[ "$value" != *[\"\\\$\`]* ]] || die "a secret contains a quote, backslash, \$ or backtick"
done

[[ -f "$STAGE_DIR/api/quillbridge.api.dll" ]] || die "$STAGE_DIR/api holds no build"
[[ -f "$STAGE_DIR/web/index.html" ]] || die "$STAGE_DIR/web holds no build"

# ─── Account and directories ─────────────────────────────────────────────────────────────────────
id -u "$APP_USER" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$APP_USER"
install -d -m 755 "$APP_DIR" "$WEB_DIR" /var/www/certbot
install -d -m 700 "$CONF_DIR"

# ─── Database ────────────────────────────────────────────────────────────────────────────────────
docker inspect -f '{{.State.Running}}' "$MSSQL_CONTAINER" 2>/dev/null | grep -q true \
    || die "the $MSSQL_CONTAINER container is not running"
[[ -f /etc/stockflow/mssql.pw ]] || die "/etc/stockflow/mssql.pw (the sa password) is missing"

if [[ ! -f "$CONF_DIR/mssql.pw" ]]; then
    # upper + lower + digits satisfies SQL Server's password policy and needs no quoting anywhere
    echo "Qb$(openssl rand -hex 24)" > "$CONF_DIR/mssql.pw"
    chmod 600 "$CONF_DIR/mssql.pw"
fi
DB_PASSWORD="$(cat "$CONF_DIR/mssql.pw")"

log "database: ensuring login, database and user"
docker exec -i -e SQLCMDPASSWORD="$(cat /etc/stockflow/mssql.pw)" "$MSSQL_CONTAINER" \
    "$SQLCMD" -C -b -S localhost -U sa <<SQL
IF SUSER_ID(N'quillbridge') IS NULL
    CREATE LOGIN [quillbridge] WITH PASSWORD = N'$DB_PASSWORD', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN [quillbridge] WITH PASSWORD = N'$DB_PASSWORD';
GO
IF DB_ID(N'quillbridge') IS NULL
    CREATE DATABASE [quillbridge];
GO
USE [quillbridge];
IF USER_ID(N'quillbridge') IS NULL
    CREATE USER [quillbridge] FOR LOGIN [quillbridge];
ALTER ROLE db_owner ADD MEMBER [quillbridge];
GO
SQL

# ─── Configuration ───────────────────────────────────────────────────────────────────────────────
log "writing $CONF_DIR/api.env"
umask 077
cat > "$CONF_DIR/api.env.new" <<ENV
ConnectionStrings__DefaultConnection="Server=127.0.0.1,1433;Database=quillbridge;User Id=quillbridge;Password=$DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=30"
Jwt__Key="$JWT_KEY"
Claude__ApiKey="$CLAUDE_API_KEY"
Cors__AllowedOrigins__0="https://$DOMAIN"
Cors__AllowedOrigins__1="http://$DOMAIN"
ENV
mv "$CONF_DIR/api.env.new" "$CONF_DIR/api.env"
umask 022

install -m 644 "$STAGE_DIR/deploy/quillbridge-api.service" /etc/systemd/system/quillbridge-api.service
systemctl daemon-reload
systemctl enable --quiet quillbridge-api

# ─── Release ─────────────────────────────────────────────────────────────────────────────────────
log "web -> $WEB_DIR"
rsync -a --delete "$STAGE_DIR/web/" "$WEB_DIR/"
chown -R root:root "$WEB_DIR"
find "$WEB_DIR" -type d -exec chmod 755 {} +
find "$WEB_DIR" -type f -exec chmod 644 {} +

log "api -> $APP_DIR"
systemctl stop quillbridge-api || true
rsync -a --delete "$STAGE_DIR/api/" "$APP_DIR/"
chown -R root:"$APP_USER" "$APP_DIR"
chmod -R u=rwX,g=rX,o= "$APP_DIR"
systemctl start quillbridge-api

# migrations and seeding run on start, so "started" is not yet "serving"
log "waiting for /api/health"
healthy=0
for attempt in {1..60}; do
    if curl -fsS --max-time 3 "http://127.0.0.1:$API_PORT/api/health" >/dev/null 2>&1; then
        log "healthy after ${attempt}s"
        healthy=1
        break
    fi
    if ! systemctl is-active --quiet quillbridge-api; then
        break
    fi
    sleep 1
done
if [[ $healthy -ne 1 ]]; then
    echo "[quillbridge] the API did not come up:" >&2
    journalctl -u quillbridge-api -n 60 --no-pager >&2
    exit 1
fi

# ─── nginx and the certificate ───────────────────────────────────────────────────────────────────
CONF=/etc/nginx/conf.d/quillbridge.conf
CERT="/etc/letsencrypt/live/$DOMAIN/fullchain.pem"

install_site() {  # $1: http | tls
    local backup=""
    [[ -f "$CONF" ]] && { backup="$(mktemp)"; cp "$CONF" "$backup"; }
    install -m 644 "$STAGE_DIR/deploy/nginx-quillbridge-app.conf" /etc/nginx/quillbridge-app.conf
    sed "s/__DOMAIN__/$DOMAIN/g" "$STAGE_DIR/deploy/nginx-quillbridge-$1.conf" > "$CONF"
    if ! nginx -t 2>&1; then
        # never leave StockFlow behind a config nginx refuses to load
        if [[ -n "$backup" ]]; then cp "$backup" "$CONF"; else rm -f "$CONF"; fi
        die "nginx rejected the $1 config; the previous one was put back"
    fi
    systemctl reload nginx
    [[ -n "$backup" ]] && rm -f "$backup"
    return 0
}

if [[ -f "$CERT" ]]; then
    log "nginx: HTTPS on $DOMAIN"
    install_site tls
else
    log "nginx: plain HTTP on $DOMAIN, requesting a certificate"
    install_site http

    if [[ -n "$CERT_EMAIL" ]]; then account=(-m "$CERT_EMAIL"); else account=(--register-unsafely-without-email); fi
    if certbot certonly --webroot -w /var/www/certbot -d "$DOMAIN" \
            --non-interactive --agree-tos --no-eff-email --keep-until-expiring "${account[@]}"; then
        log "nginx: certificate issued, switching to HTTPS"
        install_site tls
    else
        # not fatal: the site works on HTTP and the next deploy asks again
        log "certbot failed — staying on plain HTTP. Check that $DOMAIN resolves to this server."
    fi
fi

log "done — $( [[ -f "$CERT" ]] && echo https || echo http )://$DOMAIN"
