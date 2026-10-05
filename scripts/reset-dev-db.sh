#!/usr/bin/env bash
# Recrea la base LOCAL de desarrollo: la borra y, al iniciar la API, se aplican las migraciones
# y se cargan los datos de seed.development.json. Nunca ejecutar contra producción.
set -euo pipefail
cd "$(dirname "$0")/.."

set -a; source .env; set +a
database="${DATABASE_NAME:-WebProveedores}"

case "${ConnectionStrings__DefaultConnection:-}" in
  *localhost*|*127.0.0.1*) ;;
  *) echo "La cadena de conexión no apunta a localhost; no se borra nada." >&2; exit 1 ;;
esac

docker compose up -d sqlserver >/dev/null
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

echo "Base «$database» eliminada. Inicia la API para recrearla con seed.development.json:"
echo "  set -a && source .env && set +a && dotnet run --project WebProveedores.Api"
