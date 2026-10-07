#!/usr/bin/env bash
# Recrea la base LOCAL de desarrollo: detiene la API si está corriendo, borra la base y sus adjuntos,
# y al iniciar la API se aplican las migraciones y se cargan los datos de seed.development.json.
# Nunca ejecutar contra producción.
#
#   scripts/reset-dev-db.sh           # deja todo listo; inicias la API tú
#   scripts/reset-dev-db.sh --start   # además inicia la API (Development) en esta terminal
set -euo pipefail
cd "$(dirname "$0")/.."

start_api=false
[ "${1:-}" = "--start" ] && start_api=true

set -a; source .env; set +a
# La base es la de la cadena de conexión del .env (Database= o Initial Catalog=), nunca un nombre por defecto.
database=$(printf '%s' "${ConnectionStrings__DefaultConnection:-}" | tr ';' '\n' | sed -nE 's/^[[:space:]]*(Database|Initial Catalog)[[:space:]]*=[[:space:]]*//Ip' | head -1)
if [ -z "$database" ]; then
  echo "No encontré el nombre de la base en ConnectionStrings__DefaultConnection; no se borra nada." >&2
  exit 1
fi

case "${ConnectionStrings__DefaultConnection:-}" in
  *localhost*|*127.0.0.1*) ;;
  *) echo "La cadena de conexión no apunta a localhost; no se borra nada." >&2; exit 1 ;;
esac

# Una API corriendo mantiene la conexión y no recrearía la base hasta reiniciarse.
api_pids=$(pgrep -f "WebProveedores.Api" || true)
if [ -n "$api_pids" ]; then
  echo "Deteniendo la API en ejecución…"
  kill $api_pids 2>/dev/null || true
  sleep 2
fi

# Si el contenedor ya existe (por ejemplo, creado desde otra copia del proyecto) se usa ese.
if [ -z "$(docker ps -q -f name=^web-proveedores-sqlserver$)" ]; then
  docker start web-proveedores-sqlserver >/dev/null 2>&1 || docker compose up -d sqlserver >/dev/null
fi
echo "Esperando a SQL Server…"
until docker exec web-proveedores-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; do sleep 2; done

docker exec web-proveedores-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "
IF DB_ID('$database') IS NOT NULL
BEGIN
  ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [$database];
END" >/dev/null
# Adjuntos de la base anterior (ruta relativa a WebProveedores.Api, directorio de trabajo de `dotnet run`).
documents="${Storage__DocumentsPath:-App_Data/documents}"
case "$documents" in /*|*..*) ;; *) rm -rf -- "WebProveedores.Api/$documents" ;; esac

echo "Base «${database}» eliminada."
if $start_api; then
  echo "Iniciando la API (Development); se recrea la base con seed.development.json…"
  # Mismo puerto que espera el frontend (environment.ts → http://localhost:5080).
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5080}" \
    exec dotnet run --project WebProveedores.Api --no-launch-profile
fi
echo "Inicia la API para recrearla con seed.development.json:"
echo "  cd $(pwd) && set -a && source .env && set +a && dotnet run --project WebProveedores.Api"
echo "(o vuelve a ejecutar este script con --start)"
