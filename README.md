# Portal de Proveedores · Backend

Backend ASP.NET Core Web API para el Portal de Proveedores.

## Requisitos

- .NET SDK 10.
- Docker Desktop con Docker Compose.
- `dotnet-ef` para aplicar migraciones.

## Stack

ASP.NET Core Web API, Entity Framework Core 10, SQL Server 2022 en Docker, JWT y Swagger.

## Configuración local

```bash
cp .env.example .env
```

Revisar en `.env` la conexión SQL, la llave JWT y las credenciales del administrador inicial. El archivo `.env` es local y no debe versionarse.

## Levantar SQL Server

```bash
docker compose up -d sqlserver
docker compose ps
```

Esperar a que el servicio aparezca como `healthy`.

## Base de datos

Si no tienes la herramienta instalada:

```bash
dotnet tool install --global dotnet-ef
```

Aplicar migraciones:

```bash
dotnet ef database update --project WebProveedores.Infrastructure --startup-project WebProveedores.Api
```

La conexión se toma de `ConnectionStrings__DefaultConnection`. En Windows PowerShell se puede definir con `$env:ConnectionStrings__DefaultConnection = '...'`.

## Ejecutar la API

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
set -a
source .env
set +a
dotnet run --project WebProveedores.Api
```

En Windows PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5080'
dotnet run --project WebProveedores.Api
```

Endpoints locales:

- Health: [http://localhost:5080/health](http://localhost:5080/health)
- Swagger: [http://localhost:5080/swagger](http://localhost:5080/swagger)
- OpenAPI: [http://localhost:5080/swagger/v1/swagger.json](http://localhost:5080/swagger/v1/swagger.json)

## Administrador inicial

`AdminBootstrapper` crea el administrador si el correo configurado no existe. No modifica usuarios existentes ni reinicia contraseñas.

```text
Correo: admin@naviera.local
Contraseña: valor de `BootstrapAdmin__Password` en el archivo `.env`
```

Roles disponibles: `Proveedor`, `Área Usuaria`, `CxP` y `Administrador`.

## Comandos útiles

```bash
dotnet build WebProveedores.slnx --no-restore
dotnet test WebProveedores.slnx
dotnet format whitespace --folder
docker compose logs -f sqlserver
docker compose down
```

## Estructura

```text
WebProveedores.Api            # Controllers, configuración y middleware
WebProveedores.Application    # Contratos y casos de uso
WebProveedores.Domain         # Entidades y reglas del dominio
WebProveedores.Infrastructure # EF Core, autenticación y persistencia
WebProveedores.Tests          # Pruebas automatizadas
```

Si la API no conecta con SQL Server, verificar Docker, el estado `healthy` de `docker compose ps` y que la contraseña de `.env` coincida con `ConnectionStrings__DefaultConnection`. Para errores desde el frontend, confirmar el puerto `5080` y CORS para `http://localhost:4200`.
