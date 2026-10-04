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

Todavía no implementado en backend: documentos, archivos adjuntos, validación SUNAT/Sertica, historial, aprobaciones, contabilización y workflows persistentes. Esas partes existen principalmente como prototipo frontend.

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
- Tests: 6 passed.
- `/health`: `Healthy`.
- SQL Server Docker: `healthy`.

## Próximos pasos recomendados

1. Crear entidades y migraciones de documentos, archivos, validaciones, historial y aprobaciones.
2. Definir contratos de Application para documentos antes de crear controllers.
3. Implementar almacenamiento de archivos con límites, MIME/extensiones permitidos y nombres seguros.
4. Conectar el frontend de documentos reemplazando mocks por HTTP.
5. Implementar aprobaciones por área/usuario y estados transaccionales.
6. Completar administración multirol, áreas y permisos.
7. Añadir pruebas de integración de login, registro, activación y recuperación.
8. Agregar rate limiting, auditoría y manejo de errores operativo antes de producción.
