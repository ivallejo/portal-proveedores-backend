# Portal de Proveedores · Backend

Backend ASP.NET Core Web API para el Portal de Proveedores.

> Para continuar el desarrollo con otro agente, leer primero [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md). Describe la arquitectura, contratos, seguridad, base de datos, configuración y el límite actual entre backend implementado y módulos que siguen en mock.

## Levantar todo con un comando

Con los dos repos clonados uno al lado del otro (`portal-proveedores-backend` y `portal-proveedores-mock`):

```bash
scripts/dev-up.sh
```

Verifica las herramientas, crea el `.env` desde `.env.example` si falta (luego hay que completar SAP y SMTP), inicia SQL Server en Docker, la API en `http://localhost:5080` (aplica migraciones y carga el seed) y el frontend en `http://localhost:4200`. Ctrl+C detiene la API y el frontend. Opciones: `--api-only` (sin frontend) y `FRONT_DIR=/ruta` si el frontend está en otra carpeta. El log de la API queda en `.dev-logs/api.log`.

## Stack y versiones

| Componente | Versión |
|---|---|
| .NET / ASP.NET Core Web API | 10 LTS (`net10.0`) |
| Entity Framework Core (SQL Server) | 10.0 |
| SQL Server | 2022 (en desarrollo, en Docker) |
| Autenticación | JWT Bearer |
| Documentación de la API | OpenAPI + Swagger (Swashbuckle 10.2) |
| Almacenamiento de adjuntos | Azure Blob Storage (`Azure.Storage.Blobs` 12.30); Azurite en desarrollo |
| Resiliencia HTTP (SAP) | `Microsoft.Extensions.Http.Resilience` 10.10 |
| PDF de órdenes de pago | PDFsharp 6.2 |
| Pruebas | xUnit 2.9, NetArchTest 1.3, Testcontainers 4.15 (SQL Server y Azurite) |

## Requisitos

- .NET SDK 10.
- Docker Desktop con Docker Compose (SQL Server y Azurite en desarrollo; también lo usan las pruebas de integración).
- `dotnet-ef` para aplicar migraciones.

## Comandos

```bash
dotnet restore WebProveedores.slnx
dotnet build WebProveedores.slnx -c Release --no-restore
dotnet test WebProveedores.slnx -c Release --no-build      # requiere Docker para las pruebas de integración
dotnet publish WebProveedores.Api -c Release -o publish/api --no-restore
```

**Artefacto:** `publish/api/` contiene la API lista para IIS (`WebProveedores.Api.dll` y `web.config` generado por el SDK).

**Script de base de datos** (para que TI lo ejecute sin dar permisos de DDL a la aplicación). Es idempotente: se puede correr sobre una base en cualquier versión anterior. No se conecta a la base, pero necesita que `ConnectionStrings__DefaultConnection` esté definida (sirve el `.env` o cualquier valor):

```bash
dotnet ef migrations script --idempotent --project WebProveedores.Infrastructure --startup-project WebProveedores.Api -o publish/db/migraciones.sql
```

## Variables de configuración

Se leen de `appsettings.json`, `appsettings.{Ambiente}.json` y variables de entorno (`Seccion__Clave`). Los valores secretos nunca van al repositorio; en local están en `.env` (plantilla en `.env.example`).

| Variable | Secreto | Finalidad |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | No | `Development` o `Production` |
| `ConnectionStrings__DefaultConnection` | Sí | Conexión a SQL Server |
| `Database__ApplyMigrationsOnStartup` | No | Aplica migraciones al arrancar (por defecto `false`; en servidores se usa el script) |
| `Jwt__SigningKey` | Sí | Llave de firma de los tokens |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes` | No | Emisor, audiencia y duración del token |
| `Cors__AllowedOrigins__0` | No | Origen permitido del frontend (si se publica en otro dominio) |
| `Frontend__BaseUrl` | No | URL del portal, para los enlaces de los correos |
| `Sap__BaseUrl`, `Sap__Client` | No | Servidor y mandante de SAP |
| `Sap__BasicToken` | Sí | Credencial Basic (Base64) de los servicios SAP |
| `Sap__DocumentServices` | No | Servicios SAP 01/02; hoy solo `Simulated` |
| `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSsl`, `Smtp__From`, `Smtp__Username` | No | Servidor de correo |
| `Smtp__Password` | Sí | Contraseña del correo |
| `Email__Mode`, `Email__TestRecipient`, `Email__AllowSendOutsideProduction` | No | Envío real, redirigido o solo log (ver [Correo](#correo)) |
| `Seed__FilePath`, `Seed__RequireAdministrator` | No | Archivo de datos iniciales |
| `Seed__TemporaryPassword` | Sí | Contraseña temporal de los usuarios del seed |
| `Storage__Provider`, `Storage__Container`, `Storage__DocumentsPath` | No | Almacenamiento de adjuntos |
| `Storage__ConnectionString` | Sí | Cuenta de Blob Storage |
| `Security__RequireHttps`, `Security__UseForwardedHeaders` | No | HTTPS/HSTS y proxy inverso |
| `Security__MaxFailedLogins`, `Security__LockoutMinutes`, `RateLimiting__LoginPerMinute`, `RateLimiting__SensitivePerMinute` | No | Bloqueo de cuenta y límites de solicitudes |

## Integraciones externas

| Integración | Finalidad | Configuración | Red |
|---|---|---|---|
| SAP S/4HANA (`/sap/bc/zconsruc` y servicios de pagos y facturas) | Consulta de RUC del proveedor, órdenes de pago y estado de facturas | `Sap__*` | HTTP al servidor SAP (`vhnzsds4ci` desarrollo, `vhnzsps4ci` producción, puerto 8000); requiere la red corporativa o VPN |
| SAP servicios 01/02 (validación de documentos) | Validar OC y documentos contra SAP | `Sap__DocumentServices` | Pendiente de SAP; hoy simulados |
| SMTP | Correos de activación, contraseña y estado de documentos | `Smtp__*`, `Email__*` | Salida al servidor SMTP (hoy Gmail, puerto 587 TLS) |
| Azure Blob Storage | Adjuntos (XML, PDF, CDR, anexos) | `Storage__*` | HTTPS a la cuenta de almacenamiento |
| SQL Server | Persistencia | `ConnectionStrings__DefaultConnection` | Puerto del motor (1433 por defecto) |

## Despliegue en IIS

- Instalar en el servidor el **ASP.NET Core Hosting Bundle** de .NET 10.
- Copiar `publish/api/` al sitio o aplicación de IIS; el pool de aplicaciones va en «Sin código administrado».
- Definir `ASPNETCORE_ENVIRONMENT` y los secretos como variables de entorno del pool o del sitio, no en archivos del repositorio.
- Ejecutar `publish/db/migraciones.sql` antes de publicar una versión con cambios de esquema; la aplicación solo necesita permisos de lectura y escritura.
- La API no arranca si la configuración es inválida: fuera de desarrollo, sin un administrador activo en el seed; en cualquier ambiente, con `Email__Mode=Redirect` sin `Email__TestRecipient`.
- Verificar con `GET /health` (SQL Server). Swagger solo se publica en `Development`.

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

La migración `AddSupplierDocuments` agrega `Companies`, `Documents`, `DocumentItems`, `DocumentAttachments` y `DocumentEvents`. Los adjuntos se guardan en **Azure Blob Storage** (`Storage__Provider=AzureBlob`); en desarrollo, en **Azurite**, el emulador oficial, que `docker compose` y `scripts/dev-up.sh` levantan en el puerto 10000 (`Storage__ConnectionString=UseDevelopmentStorage=true`). Para ver los archivos: Azure Storage Explorer → *Local storage emulator*. Con `Storage__Provider=Local` se guardan en disco (`Storage__DocumentsPath`). Para pasar los archivos del disco a Blob: `dotnet run --project WebProveedores.Api -- migrate-storage`. Detalle de endpoints y reglas en [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md#módulo-de-documentos).

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
