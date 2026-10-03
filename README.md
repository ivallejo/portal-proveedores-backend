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

Para habilitar el envío de claves por Gmail, configurar una contraseña de aplicación (no la contraseña normal de Gmail) en estas variables del `.env`:

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__Username=tu-cuenta@gmail.com
Smtp__Password=tu-app-password-de-gmail
Smtp__From=tu-cuenta@gmail.com
Smtp__EnableSsl=true
Smtp__RedirectEnabled=true
Smtp__TestRecipient=tu-correo-de-pruebas@gmail.com
```

Mientras `Smtp__RedirectEnabled=true`, ningún correo se envía al destinatario real: todos se redirigen a `Smtp__TestRecipient`. El asunto se marca como `[PRUEBA SMTP]` y el cuerpo conserva el destinatario original para facilitar las pruebas. Antes de pasar a un ambiente real, cambiar el flag a `false` o eliminarlo.

Para consultar la información del proveedor durante el registro online, configurar también el servicio SAP:

```env
Sap__BaseUrl=http://vhnzsps4ci.sap.navitranso.com:8000
Sap__Client=200
Sap__BasicToken=tu-token-base64-de-sap
```

`Sap__BasicToken` debe contener únicamente el valor Base64 de la credencial Basic. No debe incluirse en el repositorio ni en el frontend.

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

El registro online consulta el RUC en SAP mediante `GET /sap/bc/zconsruc`. La API devuelve la razón social y el correo ofuscado para la pantalla de registro. Después de aceptar los términos, `POST /api/auth/request-access-key` vuelve a consultar SAP, crea o actualiza el proveedor en SQL Server y envía una clave temporal por SMTP.

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
