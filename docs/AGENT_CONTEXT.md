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
- Administración de usuarios: multirol, área, sociedades, activación y desbloqueo.
- SQL Server en Docker, migraciones EF Core, Swagger y health check.
- Módulo de documentos: registro Con OC, Sin OC y documentos especiales, adjuntos en disco, historial, aprobación (aprobar, rechazar, reasignar) y Cuentas por pagar (rechazar, observar). Ver «Módulo de documentos».

Todavía no implementado: servicios SAP 01/02 reales (hay un simulador), proceso diario de contabilización (Servicio 03, fase posterior), órdenes de compra/pago y estado de factura, y workflows persistentes.

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

Arquitectura hexagonal (puertos y adaptadores):

- `Application` **solo** depende de `Domain` y de abstracciones (`Microsoft.Extensions.DependencyInjection.Abstractions`, `Logging.Abstractions`). No conoce EF Core, ASP.NET, JWT, `IConfiguration` ni el hasher: los usa a través de puertos en `Application/Abstractions` (`IUserRepository`, `IPasswordTokenRepository`, `IReferenceDataReader`, `IUnitOfWork`, `IDocumentRepository`, `ITokenIssuer`, `IPasswordHasher`, `IEmailSender`, `IProviderDirectory`, `ISapDocumentGateway`, `IFileStorage`, `IPdfMerger`). La configuración llega como registros tipados (`PortalSettings`, `LoginLockoutSettings`) y la hora por `TimeProvider`.
- `Infrastructure` implementa los puertos y se registra con `AddInfrastructure(configuration, isProduction)`; `Application` con `AddApplication(...)`. `Program.cs` solo compone y arma el pipeline HTTP.
- `Api` traduce HTTP ↔ casos de uso. El usuario de la petición se obtiene de `ICurrentUser` (no leer claims en los controladores).
- Un servicio por caso de uso (S/I de SOLID): `LoginService`, `PasswordService`, `ProviderRegistrationService`, `AdminUserService`; en documentos `DocumentCatalogService`, `DocumentRegistrationService`, `DocumentQueryService`, `DocumentApprovalService`, `DocumentAccountingService`, con piezas internas compartidas (`DocumentAccess` para roles/sociedades, `DocumentFiles`, `DocumentNotifier`, `DocumentMapper`).

```text
Controller -> ICurrentUser + servicio de caso de uso (Application)
                 -> puertos (Application/Abstractions) <- adaptadores (Infrastructure)
                 -> entidades y reglas (Domain)
```

No colocar consultas EF, SMTP, HttpClient SAP, JWT ni lógica de hash dentro de controllers ni de `Application`.

## Estructura importante

```text
WebProveedores.Api/
├── Controllers/ (Auth, AdminUsers, Documents, Catalog, DocumentForms)
├── Infrastructure/ (CurrentUser, Policies, RateLimitPolicies, GlobalExceptionHandler)
└── Program.cs

WebProveedores.Application/
├── DependencyInjection.cs              # AddApplication
├── Abstractions/ (Auth, Persistence, Documents, Providers)
├── Auth/ (LoginService, PasswordService, ProviderRegistrationService, PasswordPolicy, AuthSettings, contratos y plantillas)
├── Admin/AdminUserService.cs
└── Documents/ (servicios por caso de uso, DocumentAccess, DocumentFiles, DocumentNotifier, DocumentMapper, UblDocumentReader)

WebProveedores.Domain/
├── Entities/ (AppUser, UserEmail, UserRole, UserCompany, Role, Area, PasswordResetToken, SecurityCatalog)
└── Documents/ (SupplierDocument con sus reglas de estado, Company, enums)

WebProveedores.Infrastructure/
├── DependencyInjection.cs              # AddInfrastructure
├── Auth/ (JwtTokenIssuer, JwtSettings, SessionClaims, IdentityPasswordHasher)
├── Persistence/ (AppDbContext, Configurations, EfUserRepository, EfPasswordTokenRepository, EfReferenceDataReader, EfUnitOfWork, EfDocumentRepository, seeder)
├── Providers/SapProviderClient.cs
├── Documents/ (LocalFileStorage, PdfSharpMerger, MockSapDocumentGateway)
├── Email/ (EmailSettings, SmtpEmailSender, RedirectingEmailSender, LogEmailSender)
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
Seed__FilePath
Seed__TemporaryPassword
Frontend__BaseUrl
Sap__BaseUrl
Sap__Client
Sap__BasicToken
Storage__DocumentsPath
Seed__FilePath
Seed__TemporaryPassword
Security__RequireHttps
Security__UseForwardedHeaders
Security__MaxFailedLogins
Security__LockoutMinutes
RateLimiting__LoginPerMinute
RateLimiting__SensitivePerMinute
Smtp__Host
Smtp__Port
Smtp__Username
Smtp__Password
Smtp__From
Smtp__EnableSsl
Email__Mode
Email__TestRecipient
Email__AllowSendOutsideProduction
```

Con `Email__Mode=Redirect`, todo correo va a `Email__TestRecipient`; el asunto se marca `[PRUEBA]` y el HTML conserva el destinatario original. Para Gmail se usa una contraseña de aplicación. Ver «Servicios externos».

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
Application/Documents     Servicios por caso de uso (catálogo, registro, consultas, aprobación, contabilidad), UblDocumentReader (XML UBL 2.1 sin DTD), contratos y plantillas de correo
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

`INTERNAL_USER` («Usuario interno») se agregó al catálogo de seguridad; `ReferenceDataSeeder` crea los roles del catálogo al iniciar.

Endpoints (JWT obligatorio, enums en texto):

| Método | Endpoint | Uso |
|---|---|---|
| GET | `/api/catalog/companies` | Sociedades activas |
| GET | `/api/catalog/areas` | Áreas con sus aprobadores activos |
| POST | `/api/documents/orders/validate` | Servicio 01: valida la orden (422 si no existe) |
| POST | `/api/documents` | Registro Con OC / Sin OC (multipart: `EntryType`, `CompanyCode`, `IsPettyCash`, `OrderType`, `OrderNumber`, `ApproverId`, `Xml`, `Pdf`, `Cdr`, `Extras[]`) |
| POST | `/api/documents/special` | Documento especial (multipart, solo `INTERNAL_USER` o admin) |
| GET | `/api/documents?inbox=Approvals\|Accounting\|Mine&ruc&status&page&pageSize` | Bandejas con `countsByStatus` para los KPI |
| GET | `/api/documents/{id}` | Detalle con ítems, adjuntos e historial |
| GET | `/api/documents/{id}/attachments/{attachmentId}` | Descarga de un adjunto |
| POST | `/api/documents/{id}/approve` · `/reject` · `/reassign` | Acciones del aprobador asignado (`reassign` exige `approverId` y `reason`) |
| POST | `/api/documents/{id}/accounting/reject` · `/accounting/observe` | Acciones de Cuentas por pagar |

Reglas que aplica el servidor (no confía en el navegador):

- Lee el XML (UBL 2.1) con DTD prohibido; el número, importes, ítems y emisor salen del XML.
- Un proveedor solo registra documentos emitidos por su RUC. Si la sociedad tiene RUC, el receptor del XML debe coincidir.
- CDR obligatorio salvo serie que empieza con «E». Archivos ≤ 5 MB, extensión permitida y firma de contenido (`%PDF`, `PK`, XML).
- Duplicidad por (RUC emisor, número): índice único en BD → 422.
- Sin OC (proveedor o usuario interno) → *PendingApproval* con área y aprobador elegidos, salvo **Caja Chica** (`IsPettyCash`) → *PendingAccounting* sin aprobador. Solo `INTERNAL_USER` o admin pueden marcar Caja Chica, y solo en Sin OC. Con OC y especiales → *PendingAccounting*.
- Los PDF extras se consolidan en un solo adjunto `Anexos_{número}.pdf` (`IPdfMerger`, PDFsharp). Un PDF dañado o con contraseña rechaza el registro y no deja archivos.
- Solo el aprobador asignado (o el admin) aprueba, rechaza o reasigna. Aprobar exige N° de pedido o de viaje; reasignar exige motivo (queda en el historial y en el correo al nuevo aprobador).
- Los correos (aprobador, rechazo, observación) no revierten la acción si fallan; se registran en el log.

Errores: `ArgumentException` → 400, `UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404, `InvalidOperationException` de negocio → 409, `DocumentRejectedException` → 422. Todas las respuestas de error incluyen `message`.

Documentos especiales: van directo a *PendingAccounting* (confirmado en los flujos actualizados). El estado `Accounted` (Contabilizado) existe en el modelo pero los flujos actuales no lo usan; el Servicio 03 queda para una fase posterior.

### Datos iniciales (seed)

Los datos iniciales se cargan con `ReferenceDataSeeder` al arrancar, **no con migraciones** (las migraciones solo cambian el esquema). Es idempotente: solo crea lo que falta y nunca modifica ni borra registros existentes.

- Siempre: las sociedades base con su RUC (confirmados con SAP): 1001 Naviera Transoceánica S.A. (20522163890), 1002 Petrolera Transoceánica S.A. (20100126606), 1003 Naviera Petral S.A. (20511922578) y 1007 Representaciones Navieras y Aduaneras S.A.C. · RENADSA (20100245796), con su correo de facturación (`Company.BillingEmail`, recepción de comprobantes electrónicos; RENADSA aún sin correo), y los roles del catálogo. No hay administrador genérico: los administradores vienen del seed, y sin ninguno activo la API no arranca fuera de desarrollo (`Seed:RequireAdministrator`).
- Desarrollo: `seed.development.json` (versionado, ficticio: áreas Operaciones, Logística, Compras, Finanzas, Contabilidad y usuarios `prueba.admin`, `prueba.aprobador`, `prueba.aprobador2`, `prueba.cxp`, `prueba.interno`). `scripts/reset-dev-db.sh` borra la base local (solo si la conexión apunta a localhost) para recrearla.
- Sociedades por usuario (`UserCompanies`): cada usuario trabaja con una o varias sociedades (`"companies"` en el seed; si se omite, todas). Registrar documentos solo admite sus sociedades; las bandejas de Documentos y Contabilización y el detalle se filtran por ellas (lo propio —registrado, emitido con su RUC o asignado a él— siempre es visible). El aprobador elegido debe trabajar con la sociedad del documento. El administrador ve todas. Los proveedores nuevos reciben todas. Admin: `PUT /api/admin/users/{id}`.
- Producción: `seed.production.json` (fuera de git) sobre una base nueva. `Seed:FilePath` relativo se busca en el directorio actual y en el padre.
- Con un archivo de seed: sociedades con su RUC, áreas y **usuarios reales**. El archivo se busca en `Seed__FilePath`, o `seed.json` en el directorio actual o en el padre. Está ignorado por git porque contiene datos personales y contraseñas temporales; la plantilla es `seed.example.json`.
- Cada usuario se crea con una **contraseña temporal** (`temporaryPassword` del usuario o `Seed__TemporaryPassword`; mínimo 8 caracteres con mayúscula, minúscula y número) y la marca `MustChangePassword`.
- Si el archivo tiene errores (rol desconocido, correo inválido, área inexistente, contraseña débil…) el arranque falla y lista todos los problemas, sin crear nada.
- No hay usuarios de demostración en el código.

### Contraseña temporal

Los usuarios sembrados y los creados por el administrador (`POST /api/admin/users`) deben cambiar la clave al ingresar:

- El login devuelve `user.mustChangePassword = true` y un JWT con el claim `pwd_change`.
- Con ese claim la API responde **403 `PASSWORD_CHANGE_REQUIRED`** a todo salvo `POST /api/auth/change-password` y `GET /api/auth/me`.
- `POST /api/auth/change-password` (`currentPassword`, `newPassword`) valida la clave actual, exige que la nueva sea distinta y cumpla la política (mínimo 8, mayúscula, minúscula y número), quita la marca y devuelve una **sesión nueva** sin el claim.

## Endpoints de autenticación

Base: `/api/auth`.

| Método | Endpoint | Auth | Uso |
|---|---|---|---|
| POST | `/login` | Anónimo | Login por identifier/RUC/username/correo |
| POST | `/change-password` | JWT | Cambia la contraseña (obligatorio con clave temporal) |
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

Protecciones del acceso (todas con prueba o verificación HTTP):

- **Bloqueo temporal de cuenta:** 5 contraseñas incorrectas seguidas bloquean la cuenta 15 minutos (`Security:MaxFailedLogins`, `Security:LockoutMinutes`). Responde 429 con `Retry-After`; un ingreso correcto reinicia el contador. Un usuario inexistente responde igual que una clave mala (401) y se verifica un hash de relleno para igualar tiempos.
- **Límite de peticiones por IP:** login 10/min y endpoints públicos sensibles (`validate-ruc`, `request-access-key`, recuperación y activación) 10/min (`RateLimiting:LoginPerMinute`, `RateLimiting:SensitivePerMinute`). Responde 429.
- **Recuperación de contraseña sin enumeración:** `password-reset/request` responde siempre `{ sent: true, maskedEmail: "" }`, exista o no el RUC, y un fallo de correo solo se registra. El front muestra un texto genérico.
- **`POST /api/auth/register` solo administradores:** el alta de proveedores es por RUC (`request-access-key`).
- **Cabeceras:** `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` y `Cache-Control: no-store` en `/api`.
- **HTTPS/HSTS:** activar con `Security:RequireHttps=true` en producción.
- **Proxy inverso:** con `Security:UseForwardedHeaders=true` se toma la IP real de `X-Forwarded-For` (sin esto, el límite por IP verá solo la IP del proxy). Activar solo si el proxy es de confianza y el backend no es accesible directamente.
- **SAP:** el cliente usa reintentos con espera, corte de circuito y tiempos máximos (`AddStandardResilienceHandler`). El uso de HTTPS contra SAP depende de la URL configurada en `Sap__BaseUrl`.

Pendiente de producción: `AllowedHosts` restringido, secretos fuera del `.env` (gestor de secretos), CORS por ambiente y auditoría de accesos.

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
20261004193728_AddPettyCashFlag
20261004233933_AddLoginLockout
20261005032256_AddMustChangePassword
```

No borrar migraciones ni el volumen Docker para resolver errores de conexión. Primero revisar contenedor, credenciales y connection string.

## Servicios externos

- SAP o correo caídos responden **503** con un mensaje para reintentar (`ServiceUnavailableException`), no 500. La consulta de RUC requiere la VPN hacia `Sap:BaseUrl`.
- Correo (`Infrastructure/Email`): `Email:Mode` = `Send` | `Redirect` | `Log`, resuelto y validado al arrancar por `EmailSettings` (por defecto `Send` en producción y `Redirect` fuera de ella, o `Log` si no hay `Email:TestRecipient`). `RedirectingEmailSender` envuelve a `SmtpEmailSender` y entrega todo al buzón de pruebas. `Send` fuera de producción exige `Email:AllowSendOutsideProduction=true`. Se aceptan los nombres antiguos `Smtp:RedirectEnabled`/`Smtp:TestRecipient`. Hoy el transporte es Gmail SMTP; otro proveedor = otra implementación de `IEmailSender`.

## Errores

`Application/Errors.cs`: `ValidationException` (400), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` (409); en el dominio `DomainRuleException` (409). Además `DocumentRejectedException` (422), `AccountLockedException` (429 + Retry-After) y `ServiceUnavailableException` (503). `GlobalExceptionHandler` solo traduce esos tipos: cualquier otra excepción es 500 con mensaje genérico. No usar `ArgumentException`/`InvalidOperationException` para errores de negocio, ni try/catch en los controladores.

## Entidades

`AppUser` y `SupplierDocument` tienen `private set` y constructor privado (EF los materializa igual). Se crean con `AppUser.Create(...)` y `SupplierDocument.Register(...)` y cambian solo con sus métodos (`RecordFailedLogin`, `SetPassword`, `SetRoles`, `SetCompanies`…; `Approve`, `Reassign`, `Reject`, `Observe`, `AddItem`, `AddAttachment`). `Register` aplica las reglas de entrada (Con OC con orden, Sin OC con aprobador salvo Caja Chica, especiales a contabilización) y genera el historial.

## SAP 01/02

`Sap:DocumentServices` elige la implementación de los servicios 01/02. Hoy solo existe `Simulated` (`MockSapDocumentGateway`); fuera de producción es el valor por defecto, en producción hay que declararlo explícitamente o la API no arranca. El log de arranque avisa que están simulados.

## Orden de pago y Estado de factura

Consultas en línea a SAP con el mismo `Sap:BaseUrl` (desarrollo `vhnzsds4ci`, producción `vhnzsps4ci`; `appsettings.json` no trae URL, la de desarrollo está en `appsettings.Development.json`) (puerto `ISapPaymentsGateway`, adaptador `SapPaymentsClient`):

- `GET /api/payment-orders?from=&to=&ruc=&company=` → servicio `zconsopago` (órdenes con sus comprobantes, retención, detracción y constancias).
- `GET /api/invoices?from=&to=&ruc=&company=&number=` → servicio `zconsfactu` (estado SAP tal cual: Recepcionado, Pagado, Documento Anulado…).
- SAP recibe `RUC`, `fechad` y `fechah` (dd/MM/yyyy) y devuelve fechas `yyyyMMdd` («00000000» = vacía) e importes como texto. El número llega como «01-F008-…» (prefijo = tipo SUNAT).
- Acceso (`PaymentQueryService`, política `Payments.View`): el proveedor siempre consulta su RUC; CxP y el administrador deben indicar `ruc`. Fuera del administrador solo se ven las sociedades asignadas. Rango máximo 3 años.
- La orden trae el código de sociedad (`zbukr`); la factura solo el RUC de la sociedad (`ruc_adqui`), así que filtrar facturas por sociedad requiere que la sociedad tenga RUC en el catálogo.

## Configuración › Sociedades y Áreas

`IOrganizationService` (`OrganizationController`, solo administrador):

```text
GET   /api/admin/companies?search=&active=      # con número de áreas y de usuarios
POST  /api/admin/companies                      # código 2-5 alfanumérico único, RUC 11 dígitos con 20 y único, razón social, correo
PUT   /api/admin/companies/{id}
PATCH /api/admin/companies/{id}/status          # desactivar: sale de los filtros y no se registran documentos; áreas e historial se conservan
GET   /api/admin/areas?search=&active=&companyId=
POST  /api/admin/areas                          # sociedad activa, nombre único dentro de la sociedad, descripción opcional
PUT   /api/admin/areas/{id}
PATCH /api/admin/areas/{id}/status              # desactivar: ya no se asigna a nuevos usuarios; los actuales la conservan
```

- Cada área pertenece a una sociedad (`Area.CompanyId`); su clave (`Area.CodeFor`) es única dentro de la sociedad. En el seed: `{ "name", "company", "description" }`; un usuario indica el área por nombre o, si se repite entre sociedades, como `CÓDIGO:Nombre`.
- No se elimina nada: solo se activa o desactiva.
- El correo de facturación de la sociedad va **en copia** de los avisos al proveedor (rechazo y observación). `IEmailSender` acepta `copyTo`; en modo Redirect las copias no salen y se indican en el aviso del correo de prueba.

## Mi perfil

`IProfileService` (`ProfileController`, cualquier usuario con sesión):

```text
GET    /api/profile                              # datos, roles, área, sociedades, correos, fechas
PUT    /api/profile                              # proveedor: businessName (≥ 3); interno: firstName + lastName (solo letras)
POST   /api/profile/emails                       # { email, type: work|billing|personal } → pendiente de verificación, envía enlace
POST   /api/profile/emails/{id}/verification     # reenvía el enlace (24 h)
POST   /api/profile/emails/{id}/primary          # solo correos verificados
DELETE /api/profile/emails/{id}                  # el principal no se elimina
POST   /api/profile/emails/verify                # sin sesión: { token } del enlace `/?emailToken=…`
```

- El cambio de contraseña sigue en `POST /api/auth/change-password` (devuelve una sesión nueva).
- Un correo solo sirve para **ingresar** si está verificado. Los correos creados por el administrador, el seed o SAP nacen verificados (la migración `ProfileEmailsAndNames` marcó los existentes).
- `AppUser.FirstName/LastName` (personal interno): `CompanyName` guarda el nombre completo para el resto del sistema.

## Configuración › Usuarios

Base: `/api/admin/users` (`AdminUserService`, policy `Users.Manage`).

```text
GET   /api/admin/users?search=&role=&status=&page=&pageSize=  # status: active|inactive|locked; incluye contadores (total, activos, bloqueados o inactivos)
GET   /api/admin/users/catalog                  # roles, áreas y sociedades (con su estado, para mostrarlas deshabilitadas)
GET   /api/admin/users/{id}
POST  /api/admin/users                          # alta: envía el enlace de activación al correo principal
PUT   /api/admin/users/{id}                     # datos, rol, área, sociedades, correos[], status, mustChangePassword
PATCH /api/admin/users/{id}/status              # activar también desbloquea
POST  /api/admin/users/{id}/password-link       # activación (si no activó la cuenta) o recuperación
GET   /api/admin/users/{id}/password-links      # historial: vigente, usado, reemplazado, vencido
```

Reglas:
- **Un rol por usuario.** Proveedor: RUC de 11 dígitos que empieza con 10 o 20 y razón social. Personal interno: DNI de 8 dígitos (es su usuario de acceso), nombres, apellidos y área (opcional solo para el administrador); el área debe ser de una de sus sociedades.
- El documento no se edita, y no se cambia una cuenta de proveedor a interna ni al revés.
- Al menos una sociedad (las inactivas no se asignan, pero quien ya las tiene las conserva) y al menos un correo, con exactamente un principal. Los correos nuevos reciben un enlace de verificación; el principal de una cuenta activada debe estar verificado. Activar la cuenta verifica el correo principal.
- Estado: Activo, Inactivo o Bloqueado (bloqueo temporal por intentos fallidos; no se asigna a mano).
- «Solicitar cambio de contraseña en el próximo inicio» = `MustChangePassword`: al ingresar se abre `/contrasena-temporal`.
- Un administrador no puede desactivarse ni quitarse su rol, y nunca queda el portal sin un administrador activo (409).
- `PasswordLinks` emite los enlaces (24 h) y reemplaza los anteriores del mismo tipo. El enlace identifica la cuenta con `ruc=` (proveedor) o `user=` (personal interno); `PasswordResetConfirmRequest` acepta `ruc` o `user`.
- El login acepta usuario, RUC, DNI o un correo verificado. En el seed, el personal interno puede tener `dni`, `firstName` y `lastName`.

## Calidad validada

```bash
dotnet build WebProveedores.slnx --no-restore
dotnet test WebProveedores.slnx --no-restore
dotnet format whitespace --folder
```

Estado validado:

- Build: 0 warnings, 0 errores.
- Tests: 69 passed. Unitarios con EF InMemory y dobles (autenticación, bloqueo, seed, contraseñas, documentos, administración, dominio, correo) y de integración contra SQL Server real con Testcontainers (`Tests/Integration`: traducción de consultas, índices únicos, migraciones). Los de integración necesitan Docker; para omitirlos: `dotnet test WebProveedores.slnx --filter "Category!=Integration"`.
- `/health`: `Healthy`.
- SQL Server Docker: `healthy`.

## Próximos pasos recomendados

1. Conectar el frontend (Registrar documentos, Documentos, Contabilización) a `api/documents` y `api/catalog`.
2. Reemplazar `MockSapDocumentGateway` por los servicios SAP 01/02 reales y agregar el proceso diario del Servicio 03.
3. Completar administración multirol y asignación de áreas (necesario para los aprobadores).
7. Añadir pruebas de integración de login, registro, activación y recuperación.
8. Agregar rate limiting, auditoría y manejo de errores operativo antes de producción.
