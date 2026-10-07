#!/usr/bin/env bash
# Levanta todo el entorno LOCAL de desarrollo con un solo comando:
#   1. Verifica las herramientas (Docker, .NET 10, Node/npm).
#   2. Crea el .env desde .env.example si no existe (luego hay que completar SAP y SMTP).
#   3. Inicia SQL Server en Docker y espera a que esté listo.
#   4. Inicia la API (Development: aplica migraciones y carga seed.development.json) y espera /health.
#   5. Instala dependencias del frontend si hace falta e inicia Angular en http://localhost:4200.
# Ctrl+C detiene la API y el frontend (SQL Server sigue corriendo en Docker).
#
#   scripts/dev-up.sh                 # backend + frontend
#   scripts/dev-up.sh --api-only      # solo SQL Server y la API
#   FRONT_DIR=/ruta/al/front scripts/dev-up.sh   # si el frontend no está en ../portal-proveedores-mock
set -euo pipefail
cd "$(dirname "$0")/.."
root=$(pwd)

api_only=false
[ "${1:-}" = "--api-only" ] && api_only=true
front_dir="${FRONT_DIR:-$root/../portal-proveedores-mock}"
api_url="http://localhost:5080"
log_dir="$root/.dev-logs"
mkdir -p "$log_dir"

say() { printf '\033[1;34m▸ %s\033[0m\n' "$1"; }
fail() { printf '\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# 1. Herramientas
say "Verificando herramientas…"
command -v docker >/dev/null || fail "Falta Docker Desktop (https://www.docker.com/products/docker-desktop)."
docker info >/dev/null 2>&1 || fail "Docker no está corriendo: abre Docker Desktop y vuelve a intentar."
command -v dotnet >/dev/null || fail "Falta el SDK de .NET 10 (https://dotnet.microsoft.com/download)."
dotnet --list-sdks | grep -q '^10\.' || fail "Se necesita el SDK de .NET 10 (dotnet --list-sdks)."
if ! $api_only; then
  command -v npm >/dev/null || fail "Falta Node.js y npm (versión en $front_dir/.nvmrc)."
  [ -f "$front_dir/package.json" ] || fail "No encontré el frontend en $front_dir (define FRONT_DIR)."
fi

# 2. Configuración
if [ ! -f .env ]; then
  cp .env.example .env
  say "Creé .env desde .env.example. Completa Sap__BasicToken y los datos de Smtp__* y Email__TestRecipient."
fi
set -a; source .env; set +a

# Puertos ocupados (otra API o frontend ya corriendo).
if lsof -iTCP:5080 -sTCP:LISTEN >/dev/null 2>&1; then fail "El puerto 5080 está en uso (¿la API ya está corriendo?)."; fi
if ! $api_only && lsof -iTCP:4200 -sTCP:LISTEN >/dev/null 2>&1; then fail "El puerto 4200 está en uso (¿el frontend ya está corriendo?)."; fi

# 3. SQL Server (si el contenedor ya existe, por ejemplo creado desde otra copia del proyecto, se usa ese)
say "Iniciando SQL Server…"
if [ -z "$(docker ps -q -f name=^web-proveedores-sqlserver$)" ]; then
  docker start web-proveedores-sqlserver >/dev/null 2>&1 || docker compose up -d sqlserver >/dev/null
fi
for _ in $(seq 1 60); do
  [ "$(docker inspect -f '{{.State.Health.Status}}' web-proveedores-sqlserver 2>/dev/null)" = healthy ] && break
  sleep 2
done
[ "$(docker inspect -f '{{.State.Health.Status}}' web-proveedores-sqlserver)" = healthy ] || fail "SQL Server no quedó listo (docker compose logs sqlserver)."

# 4. API
pids=()
# Detiene un proceso y todos sus hijos (npm → ng serve, dotnet run → API).
kill_tree() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null); do kill_tree "$child"; done
  kill "$1" 2>/dev/null || true
}
cleanup() {
  trap - EXIT INT TERM
  say "Deteniendo API y frontend…"
  for pid in "${pids[@]}"; do kill_tree "$pid"; done
}
trap cleanup EXIT
trap 'exit 130' INT TERM

say "Iniciando la API (log: .dev-logs/api.log)…"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="$api_url" \
  dotnet run --project WebProveedores.Api --no-launch-profile >"$log_dir/api.log" 2>&1 &
pids+=($!)
for _ in $(seq 1 120); do
  curl -sf "$api_url/health" >/dev/null 2>&1 && break
  kill -0 "${pids[0]}" 2>/dev/null || { tail -20 "$log_dir/api.log"; fail "La API se detuvo al iniciar (detalle arriba y en .dev-logs/api.log)."; }
  sleep 2
done
curl -sf "$api_url/health" >/dev/null || fail "La API no respondió en $api_url/health."
say "API lista: $api_url (Swagger: $api_url/swagger)"

if $api_only; then
  say "Listo. Ctrl+C para detener la API."
  wait "${pids[0]}"
  exit 0
fi

# 5. Frontend
cd "$front_dir"
if [ ! -d node_modules ]; then
  say "Instalando dependencias del frontend (npm ci)…"
  npm ci --no-audit --no-fund
fi
say "Iniciando el frontend en http://localhost:4200 (Ctrl+C detiene todo)…"
npm start &
pids+=($!)
wait "${pids[1]}"
