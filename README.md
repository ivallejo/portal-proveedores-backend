# Portal de Proveedores · Backend

Backend ASP.NET Core Web API para el Portal de Proveedores.

> Para continuar el desarrollo con otro agente, leer primero [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md). Describe la arquitectura, contratos, seguridad, base de datos, configuración y el límite actual entre backend implementado y módulos que siguen en mock.

## Levantar todo con un comando

Con los dos repos clonados uno al lado del otro (`portal-proveedores-backend` y `portal-proveedores-mock`):

```bash
scripts/dev-up.sh
```

Verifica las herramientas, crea el `.env` desde `.env.example` si falta (luego hay que completar SAP y SMTP), inicia SQL Server en Docker, la API en `http://localhost:5080` (aplica migraciones y carga el seed) y el frontend en `http://localhost:4200`. Ctrl+C detiene la API y el frontend. Opciones: `--api-only` (sin frontend) y `FRONT_DIR=/ruta` si el frontend está en otra carpeta. El log de la API queda en `.dev-logs/api.log`.

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

El archivo `.env` es local y no debe versionarse.

Revisar en `.env` la conexión SQL y la llave JWT.

### Correo

El envío es por SMTP (hoy Gmail). Usar una **contraseña de aplicación** de Gmail, no la contraseña normal, y que `Smtp__From` sea la misma cuenta que `Smtp__Username`:

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__Username=tu-cuenta@gmail.com
Smtp__Password=tu-app-password-de-gmail
Smtp__From=tu-cuenta@gmail.com
Smtp__EnableSsl=true

Email__Mode=Redirect
Email__TestRecipient=tu-correo-de-pruebas@gmail.com
```

`Email__Mode` define qué pasa con los correos:

| Modo | Uso | Comportamiento |
|---|---|---|
| `Send` | Producción (valor por defecto allí) | Envío real a cada destinatario. |
| `Redirect` | Desarrollo y QA (valor por defecto fuera de producción) | Todo va solo a `Email__TestRecipient`, con asunto `[PRUEBA]` y el destinatario original en el cuerpo. Nunca llega al proveedor. |
| `Log` | Sin SMTP | No envía nada; deja los enlaces (activación, cambio de contraseña) en el log. |

La configuración se valida al arrancar y el log indica el modo activo (`Correo: modo …`). Fuera de producción, `Send` exige `Email__AllowSendOutsideProduction=true`, para que un `.env` mal copiado nunca escriba a proveedores reales.

**Producción con Gmail:** cuenta dedicada al portal (no personal) con verificación en dos pasos y contraseña de aplicación, guardada como secreto del servidor; `Email__Mode=Send` (o sin definir) y sin `Email__TestRecipient`. Límite aproximado: 500 correos/día (cuenta gratuita) o 2 000 (Google Workspace). Para cambiar de proveedor más adelante basta otra implementación de `IEmailSender`.

Servicios SAP (consulta de RUC, órdenes de pago y estado de facturas). Desarrollo usa `vhnzsds4ci` y producción `vhnzsps4ci`; `appsettings.json` no trae URL, así que producción debe configurarla explícitamente:

```env
Sap__BaseUrl=http://vhnzsds4ci.sap.navitranso.com:8000
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

- **Desarrollo / QA**: `seed.development.json` (versionado, datos ficticios `prueba.*`). En el `.env`: `Seed__FilePath=seed.development.json` y `Seed__TemporaryPassword`. Para empezar de cero: `scripts/reset-dev-db.sh --start` (detiene la API si está corriendo, borra la base local y los adjuntos, e inicia la API en `http://localhost:5080`, que migra y carga el seed). Sin `--start` solo borra; se puede ejecutar desde cualquier carpeta con su ruta completa.
- **Producción**: base nueva + `seed.production.json` con los datos reales (ignorado por git; plantilla en `seed.example.json`), apuntado con `Seed__FilePath`. Después de la salida, los cambios se hacen desde las pantallas de Configuración, no con el seed. Nunca reutilizar la base de desarrollo.

Cada usuario del seed debe cambiar su contraseña temporal al ingresar. Detalle en [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md#datos-iniciales-seed).

## Estructura

```text
WebProveedores.Api            # Adaptador de entrada HTTP: controllers, requests con validaciones, seguridad y errores
WebProveedores.Application    # Hexágono: una carpeta por feature (puerto de entrada, caso de uso y contratos) y puertos de salida
WebProveedores.Domain         # Entidades y reglas del dominio
WebProveedores.Infrastructure # Adaptadores de salida: EF Core, JWT, SMTP, SAP y archivos
WebProveedores.Tests          # Pruebas de arquitectura, unitarias e integración con SQL Server
```

Arquitectura hexagonal estricta: la Api solo usa los puertos de entrada, que viven con su caso de uso en cada feature (`Application/<Feature>/`); los casos de uso dependen de puertos de salida (`Application/Ports/Outbound`) que implementa `Infrastructure`, un adaptador por puerto. Las reglas se verifican con pruebas de arquitectura (`WebProveedores.Tests/Architecture`). Detalle y «dónde va cada cosa» en [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md#arquitectura).

Si la API no conecta con SQL Server, verificar Docker, el estado `healthy` de `docker compose ps` y que la contraseña de `.env` coincida con `ConnectionStrings__DefaultConnection`. Para errores desde el frontend, confirmar el puerto `5080` y CORS para `http://localhost:4200`.
