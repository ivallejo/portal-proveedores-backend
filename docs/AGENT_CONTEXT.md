# Contexto técnico para agentes — Backend

Guía del estado real del backend del Portal de Proveedores. Leer antes de cambiar arquitectura, seguridad, base de datos o contratos HTTP.

## Alcance actual

Implementado:

- Login con username/RUC o correo y contraseña.
- JWT Bearer.
- Registro tradicional y Registro Online con validación de RUC contra SAP.
- Prevención de duplicidad de RUC.
- Activación inicial y recuperación de contraseña por correo.
- Usuarios, roles, áreas y múltiples correos en el modelo.
- Administración básica de usuarios.
- SQL Server en Docker, migraciones EF Core, Swagger y health check.
- Módulo de documentos: registro Con OC, Sin OC y documentos especiales, adjuntos en disco, historial, aprobación (aprobar, rechazar, reasignar) y Cuentas por pagar (rechazar, observar). Ver «Módulo de documentos».

Todavía no implementado: servicios SAP 01/02/03 reales (hay un simulador), proceso diario de contabilización (Servicio 03), consolidación de PDFs de sustento, órdenes de compra/pago y estado de factura, y workflows persistentes.

## Stack

- .NET 10 / ASP.NET Core Web API.
- Entity Framework Core 10.
- SQL Server 2022 Developer en Docker Compose.
- JWT Bearer.
- `Microsoft.AspNetCore.Identity.PasswordHasher<T>`.
- SMTP Gmail, cliente HTTP SAP, Swagger/OpenAPI y xUnit.

## Arquitectura

```text
WebProveedores.Api
    Controllers, composición DI, middleware y configuración

WebProveedores.Application
    Casos de uso, contratos e interfaces

WebProveedores.Domain
    Entidades, enums y catálogo de seguridad

WebProveedores.Infrastructure
    EF Core, SQL, migraciones, SMTP, SAP y bootstrapper

WebProveedores.Tests
    Pruebas automatizadas
```

Regla principal: `Application` no depende de EF Core. Los casos de uso dependen de `IIdentityRepository`, `IEmailSender` e `IProviderDirectory`; `Infrastructure` implementa esos puertos.

```text
Controller -> Application service -> Application abstraction
                                      -> Infrastructure adapter
                                      -> Domain entity
```

No colocar consultas EF, SMTP, HttpClient SAP ni lógica de hash dentro de controllers.

## Estructura importante

```text
WebProveedores.Api/
├── Controllers/AuthController.cs
├── Controllers/AdminUsersController.cs
├── Infrastructure/GlobalExceptionHandler.cs
└── Program.cs

WebProveedores.Application/
├── Auth/AuthService.cs
├── Auth/OnlineRegistrationService.cs
├── Auth/AuthContracts.cs
├── Auth/EmailTemplates.cs
├── Admin/AdminUserService.cs
└── Abstractions/

WebProveedores.Domain/Entities/
├── AppUser.cs
├── UserEmail.cs
├── UserRole.cs
├── Role.cs
├── Area.cs
└── PasswordResetToken.cs

WebProveedores.Infrastructure/
├── Persistence/AppDbContext.cs
├── Persistence/EfIdentityRepository.cs
├── Persistence/DatabaseInitializer.cs
├── Providers/SapProviderClient.cs
├── Auth/SmtpEmailSender.cs
├── Auth/AdminBootstrapper.cs
└── Migrations/
```

## Ejecución local

Requisitos: .NET SDK 10, Docker Desktop y `dotnet-ef`.

```bash
cp .env.example .env
docker compose up -d sqlserver
docker compose ps
```

Esperar `healthy` en SQL Server.

Migración manual:

```bash
dotnet ef database update \
  --project WebProveedores.Infrastructure \
  --startup-project WebProveedores.Api
```

Arranque:

```bash
set -a
source .env
set +a
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
dotnet run --project WebProveedores.Api
```

Endpoints:

```text
Health:  http://localhost:5080/health
Swagger: http://localhost:5080/swagger
OpenAPI: http://localhost:5080/swagger/v1/swagger.json
```

En Development, `DatabaseInitializer` aplica migraciones al iniciar si `Database:ApplyMigrationsOnStartup=true` y ejecuta el bootstrap del administrador.

## Variables de entorno

Usar `.env.example` como plantilla. Nunca versionar `.env`, contraseñas, tokens SAP, JWT reales o credenciales SMTP.

```text
MSSQL_SA_PASSWORD
MSSQL_PORT
ConnectionStrings__DefaultConnection
Jwt__SigningKey
BootstrapAdmin__Email
BootstrapAdmin__Password
BootstrapAdmin__CompanyName
Frontend__BaseUrl
Sap__BaseUrl
Sap__Client
Sap__BasicToken
Storage__DocumentsPath
DemoData__Enabled
DemoData__Password
Smtp__Host
Smtp__Port
Smtp__Username
Smtp__Password
Smtp__From
Smtp__EnableSsl
Smtp__RedirectEnabled
Smtp__TestRecipient
```

Con `Smtp__RedirectEnabled=true`, todo correo se redirige a `Smtp__TestRecipient`; el asunto se marca `[PRUEBA SMTP]` y el HTML conserva el destinatario original. Para Gmail se usa una contraseña de aplicación.

`SapProviderClient` ejecuta:

```http
GET {Sap__BaseUrl}/sap/bc/zconsruc?sap-client={Sap__Client}&ruc={ruc}
Authorization: Basic {Sap__BasicToken}
```

La respuesta SAP esperada es una lista con `stcd1`, `name1`, `name2`, `adrnr` y `correo`.

## Módulo de documentos

Capas:

```text
Domain/Documents          SupplierDocument (reglas de estado), DocumentItem, DocumentAttachment, DocumentEvent, Company
Application/Documents     DocumentService, UblDocumentReader (XML UBL 2.1 sin DTD), contratos y plantillas de correo
Application/Abstractions  IDocumentRepository, IFileStorage, ISapDocumentGateway
Infrastructure            EfDocumentRepository, LocalFileStorage, MockSapDocumentGateway, ReferenceDataSeeder
Api                       DocumentsController (api/documents), CatalogController (api/catalog)
```

Roles y políticas (claims con el código del rol):

| Política | Roles |
|---|---|
| `Documents.Register` | `PROVIDER`, `INTERNAL_USER`, `ADMINISTRATOR` |
| `Documents.Approve` | `AREA_APPROVER`, `ADMINISTRATOR` |
| `Documents.Account` | `ACCOUNTS_PAYABLE`, `ADMINISTRATOR` |

`INTERNAL_USER` («Usuario interno») se agregó al catálogo de seguridad; `AdminBootstrapper` lo crea al iniciar.

Endpoints (JWT obligatorio, enums en texto):

| Método | Endpoint | Uso |
|---|---|---|
| GET | `/api/catalog/companies` | Sociedades activas |
| GET | `/api/catalog/areas` | Áreas con sus aprobadores activos |
| POST | `/api/documents/orders/validate` | Servicio 01: valida la orden (422 si no existe) |
| POST | `/api/documents` | Registro Con OC / Sin OC (multipart: `EntryType`, `CompanyCode`, `OrderType`, `OrderNumber`, `ApproverId`, `Xml`, `Pdf`, `Cdr`, `Extras[]`) |
| POST | `/api/documents/special` | Documento especial (multipart, solo `INTERNAL_USER` o admin) |
| GET | `/api/documents?inbox=Approvals\|Accounting\|Mine&ruc&status&page&pageSize` | Bandejas con `countsByStatus` para los KPI |
| GET | `/api/documents/{id}` | Detalle con ítems, adjuntos e historial |
| GET | `/api/documents/{id}/attachments/{attachmentId}` | Descarga de un adjunto |
| POST | `/api/documents/{id}/approve` · `/reject` · `/reassign` | Acciones del aprobador asignado |
| POST | `/api/documents/{id}/accounting/reject` · `/accounting/observe` | Acciones de Cuentas por pagar |

Reglas que aplica el servidor (no confía en el navegador):

- Lee el XML (UBL 2.1) con DTD prohibido; el número, importes, ítems y emisor salen del XML.
- Un proveedor solo registra documentos emitidos por su RUC. Si la sociedad tiene RUC, el receptor del XML debe coincidir.
- CDR obligatorio salvo serie que empieza con «E». Archivos ≤ 5 MB, extensión permitida y firma de contenido (`%PDF`, `PK`, XML).
- Duplicidad por (RUC emisor, número): índice único en BD → 422.
- Sin OC del proveedor → *PendingApproval* con aprobador elegido; usuario interno → *PendingAccounting*. Con OC y especiales → *PendingAccounting*.
- Solo el aprobador asignado (o el admin) aprueba, rechaza o reasigna. Aprobar exige N° de pedido o de viaje.
- Los correos (aprobador, rechazo, observación) no revierten la acción si fallan; se registran en el log.

Errores: `ArgumentException` → 400, `UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404, `InvalidOperationException` de negocio → 409, `DocumentRejectedException` → 422. Todas las respuestas de error incluyen `message`.

Pendiente de decisión de negocio: `flujo.md` envía los documentos especiales a aprobación; la Propuesta 1 los registra directo a contabilización (implementado así en `DocumentService.RegisterSpecialAsync`).

### Datos de prueba

`ReferenceDataSeeder` crea siempre las sociedades 1001 Naviera Transoceánica, 1002 Ultratag, 1003 Petral y 1007 RENADSA (sin RUC hasta confirmarlo). Con `DemoData__Enabled=true` y `DemoData__Password` crea áreas y un usuario por rol: `colaborador`, `maria.torres`, `jorge.paredes`, `ana.rios`, `carlos.vega`, `cxp` y el proveedor `20512345678`. Nunca modifica registros existentes. No habilitar en producción.

## Endpoints de autenticación

Base: `/api/auth`.

| Método | Endpoint | Auth | Uso |
|---|---|---|---|
| POST | `/login` | Anónimo | Login por identifier/RUC/username/correo |
| POST | `/register` | Anónimo | Registro tradicional |
| POST | `/validate-ruc` | Anónimo | Duplicidad y consulta SAP |
| POST | `/request-access-key` | Anónimo | Prepara proveedor y envía activación |
| POST | `/password-reset/request` | Anónimo | Genera token y envía correo |
| POST | `/password-reset/confirm` | Anónimo | Consume token de recuperación |
| POST | `/activation/confirm` | Anónimo | Consume token de activación |
| GET | `/me` | JWT | Usuario autenticado |

### Registro Online

1. Normaliza un RUC de 11 dígitos.
2. Consulta si ya existe en SQL.
3. Si existe, responde `409` con `El usuario ya se encuentra registrado.`.
4. Si no existe, consulta SAP.
5. `request-access-key` vuelve a consultar SAP.
6. Crea el usuario con username/RUC y rol proveedor, o actualiza un registro incompleto.
7. Genera un token de activación criptográficamente seguro.
8. Guarda solo SHA-256 del token en `PasswordResetTokens`.
9. Envía el enlace al frontend.
10. La confirmación valida propósito, expiración y uso único; después guarda la contraseña con `PasswordHasher<AppUser>`.

Si el proveedor tiene `PasswordSetAtUtc`, no se genera otra activación y debe usar recuperación de contraseña.

## Seguridad

- Nunca se guardan contraseñas en texto plano.
- `PasswordHasher<AppUser>` usa el formato seguro de ASP.NET Identity.
- Los tokens se generan con bytes criptográficamente seguros.
- Solo se almacena el hash SHA-256 del token.
- Cada token tiene `Purpose`, expiración y `UsedAtUtc`.
- JWT contiene `sub`, email, username, RUC y claims de rol.
- Las políticas de autorización se configuran en `Program.cs`.
- CORS permite el frontend local configurado.
- No registrar secretos ni tokens en logs.

## Modelo de datos

- `Users`: identidad, username, RUC opcional, empresa, área, estado y hashes.
- `UserEmails`: uno o varios correos, con correo principal.
- `Roles`: catálogo de roles.
- `UserRoles`: relación muchos-a-muchos.
- `Areas`: áreas organizacionales.
- `PasswordResetTokens`: hash, propósito, expiración y consumo.

Migraciones actuales:

```text
20261003165656_InitialSecurity
20261003174951_AddPasswordSetAt
20261003193747_AddPasswordTokenPurpose
20261004181709_AddSupplierDocuments
```

No borrar migraciones ni el volumen Docker para resolver errores de conexión. Primero revisar contenedor, credenciales y connection string.

## Administración de usuarios

Base: `/api/admin/users`. Requiere policy `Users.Manage`.

```text
GET   /api/admin/users
POST  /api/admin/users
PUT   /api/admin/users/{id}/role
PATCH /api/admin/users/{id}/status
```

El modelo soporta múltiples roles, pero el endpoint actual `AssignRole` reemplaza la colección por un rol único. Antes de implementar edición multirol real, cambiar el contrato a una lista y proteger el último administrador activo.

## Calidad validada

```bash
dotnet build WebProveedores.slnx --no-restore
dotnet test WebProveedores.slnx --no-restore
dotnet format whitespace --folder
```

Estado validado:

- Build: 0 warnings, 0 errores.
- Tests: 17 passed (autenticación y documentos).
- `/health`: `Healthy`.
- SQL Server Docker: `healthy`.

## Próximos pasos recomendados

1. Conectar el frontend (Registrar documentos, Documentos, Contabilización) a `api/documents` y `api/catalog`.
2. Reemplazar `MockSapDocumentGateway` por los servicios SAP 01/02 reales y agregar el proceso diario del Servicio 03.
3. Consolidar los PDF de sustento de Sin OC en un solo archivo.
4. Completar administración multirol y asignación de áreas (necesario para los aprobadores).
7. Añadir pruebas de integración de login, registro, activación y recuperación.
8. Agregar rate limiting, auditoría y manejo de errores operativo antes de producción.
