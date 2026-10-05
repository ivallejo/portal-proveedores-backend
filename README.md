# Portal de Proveedores · Backend

Backend ASP.NET Core Web API para el Portal de Proveedores.

> Para continuar el desarrollo con otro agente, leer primero [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md). Describe la arquitectura, contratos, seguridad, base de datos, configuración y el límite actual entre backend implementado y módulos que siguen en mock.

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

## Administradores

No hay un administrador genérico: los administradores son usuarios nominales del archivo de seed (rol `ADMINISTRATOR`), con contraseña temporal que deben cambiar al ingresar. En desarrollo es `prueba.admin` (`seed.development.json`).

Si no queda ningún administrador activo, la API no arranca fuera de desarrollo (`Seed__RequireAdministrator`, por defecto `true`). Para recuperar el acceso, agrega un administrador al seed y reinicia: el seed crea los usuarios que falten.

Roles disponibles: `Proveedor`, `Usuario interno`, `Aprobador de área`, `Gestor de cuentas por pagar` y `Administrador`.

## Comandos útiles

```bash
dotnet build WebProveedores.slnx --no-restore
dotnet test WebProveedores.slnx
dotnet format whitespace --folder
docker compose logs -f sqlserver
docker compose down
```

## Modelo de datos inicial

La primera migración crea un modelo de seguridad normalizado:

- `Users`: identidad, username, empresa, RUC, área y estado.
- `UserEmails`: correos asociados a un usuario, con correo principal y soporte para múltiples correos.
- `Roles`: catálogo de roles del sistema.
- `UserRoles`: relación muchos-a-muchos entre usuarios y roles.
- `Areas`: áreas organizacionales a las que pueden pertenecer los usuarios internos.
- `PasswordResetTokens`: tokens de recuperación con expiración y uso controlado.

Los roles iniciales son `Proveedor`, `Usuario interno`, `Aprobador de área`, `Gestor de cuentas por pagar` y `Administrador`.

La migración `AddSupplierDocuments` agrega `Companies`, `Documents`, `DocumentItems`, `DocumentAttachments` y `DocumentEvents`. Los adjuntos se guardan en disco (`Storage__DocumentsPath`, por defecto `App_Data/documents`). Detalle de endpoints y reglas en [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md#módulo-de-documentos).

Datos iniciales por entorno (el seed solo **crea** lo que falta; nunca modifica ni borra):

- **Desarrollo / QA**: `seed.development.json` (versionado, datos ficticios `prueba.*`). En el `.env`: `Seed__FilePath=seed.development.json` y `Seed__TemporaryPassword`. Para empezar de cero: `./scripts/reset-dev-db.sh` (borra la base local y los adjuntos; al iniciar la API se migra y se carga el seed).
- **Producción**: base nueva + `seed.production.json` con los datos reales (ignorado por git; plantilla en `seed.example.json`), apuntado con `Seed__FilePath`. Después de la salida, los cambios se hacen desde las pantallas de Configuración, no con el seed. Nunca reutilizar la base de desarrollo.

Cada usuario del seed debe cambiar su contraseña temporal al ingresar. Detalle en [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md#datos-iniciales-seed).

## Estructura

```text
WebProveedores.Api            # Controllers, configuración y middleware
WebProveedores.Application    # Contratos y casos de uso
WebProveedores.Domain         # Entidades y reglas del dominio
WebProveedores.Infrastructure # EF Core, autenticación y persistencia
WebProveedores.Tests          # Pruebas automatizadas
```

La capa `Application` no depende de EF Core. Los casos de uso utilizan abstracciones como `IIdentityRepository`, `IEmailSender` e `IProviderDirectory`; sus implementaciones viven en `Infrastructure`.

Si la API no conecta con SQL Server, verificar Docker, el estado `healthy` de `docker compose ps` y que la contraseña de `.env` coincida con `ConnectionStrings__DefaultConnection`. Para errores desde el frontend, confirmar el puerto `5080` y CORS para `http://localhost:4200`.
