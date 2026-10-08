# Plan: reorganización a arquitectura hexagonal estricta

Objetivo: que la estructura de carpetas y proyectos haga visibles los **puertos de entrada (inbound)**, los **puertos de salida (outbound)** y los **adaptadores**, aplicando SRP, ISP y DIP. Regla transversal: **un tipo por archivo** (ningún `record`, `enum` o excepción dentro del archivo de una interfaz o clase).

Cada paso deja la solución compilando y las pruebas en verde, y se cierra con un commit propio. No cambia el comportamiento ni el contrato HTTP.

## 1. Diagnóstico de la estructura actual

**Lo que ya está bien (se conserva):**

- Las dependencias entre proyectos son correctas: `Domain` ← `Application` ← `Infrastructure` / `Api`. `Application` no referencia EF Core, ASP.NET ni JWT.
- Los casos de uso ya se consumen por interfaces (`ILoginService`, `IDocumentQueryService`…) y los adaptadores se registran en `AddInfrastructure`.
- Las reglas de estado de los documentos viven en el dominio (`SupplierDocument`).

**Problemas encontrados:**

| # | Problema | Principio | Ejemplos |
|---|---|---|---|
| P1 | Archivos con muchos tipos | SRP | `DocumentContracts.cs` (21 tipos), `AuthContracts.cs` (16), `AdminContracts.cs` (15), `DocumentPorts.cs` (11), `DocumentEnums.cs` (8), `SupplierDocument.cs` (7), `SapPaymentsClient.cs` (6) |
| P2 | `record` declarados junto a interfaces | SRP | `IUserRepository.cs` (`UserSearchResult`, `UserSearchFilter`), `ITokenIssuer.cs` (`IssuedToken`), `IProviderDirectory.cs` (`SapProviderRecord`), `ISapPaymentsGateway.cs` (4 records), `IAccessRepository.cs`, `IOrganizationRepository.cs` |
| P3 | Puertos de entrada mezclados con DTOs | SRP | `ILoginService`, `IPasswordService` e `IProviderRegistrationService` dentro de `AuthContracts.cs`; igual en Admin, Access, Profile, Organization y Payments |
| P4 | No se distinguen puertos de entrada y de salida | Hexagonal | Los de salida están en `Abstractions/`; los de entrada, dispersos en cada carpeta de feature |
| P5 | Interfaces de salida demasiado grandes | ISP | `IUserRepository` (15 métodos: lectura, escritura y unicidad). `IDocumentRepository` mezcla sociedades, aprobadores y documentos, y trae su propio `SaveChangesAsync` aunque ya existe `IUnitOfWork` |
| P6 | Excepciones en archivos de contratos | SRP | `AccountLockedException` en `AuthContracts.cs`, `DocumentRejectedException` en `DocumentContracts.cs`, 5 excepciones en `Errors.cs` |
| P7 | El adaptador de entrada depende de un adaptador de salida | DIP | `Api/Infrastructure/CurrentUser.cs` usa `Infrastructure.Auth.SessionClaims` |
| P8 | Nombres de carpetas ambiguos | Hexagonal | `Api/Infrastructure/` (es HTTP, no infraestructura); `Domain/Entities` vs `Domain/Documents` (criterios distintos); `Company` dentro de `Documents` |
| P9 | Restos de la plantilla | — | `WeatherForecastController.cs`, `WeatherForecast.cs` |
| P10 | DTOs de entrada del core con atributos de validación HTTP (`[Required]`, `[EmailAddress]`) | Hexagonal estricto | `LoginRequest`, `RegisterRequest`, `ValidateOrderRequest`… en `Application` |
| P11 | Nada impide que las reglas se rompan en el futuro | — | No hay pruebas de arquitectura |

## 2. Estructura objetivo

```text
WebProveedores.Domain/                 # Núcleo: entidades, value objects, reglas
├── Common/                            # DomainRuleException
├── Identity/                          # AppUser, Role, UserRole, UserEmail, PasswordResetToken, UserStatus…
├── Organization/                      # Company, Area, UserCompany
├── Access/                            # MenuOption, RoleMenu, MenuCatalog, MenuCatalogEntry, SecurityCatalog
└── Documents/                         # SupplierDocument, DocumentItem, DocumentAttachment, DocumentEvent,
    └── (un enum por archivo)          # DocumentData, PurchaseOrderInfo, ApproverAssignment, DocumentStatus…

WebProveedores.Application/            # Hexágono: casos de uso y puertos
├── Ports/
│   ├── Inbound/<Feature>/             # ILoginService, IDocumentQueryService… (lo que la Api invoca)
│   └── Outbound/
│       ├── Persistence/               # IUserReader, IUserWriter, IDocumentRepository, IUnitOfWork…
│       │   └── Models/                # UserSearchFilter, DocumentQuery, DocumentPage, ApproverRecord…
│       ├── Security/                  # IPasswordHasher, ITokenIssuer (+ Models/IssuedToken)
│       ├── Notifications/             # IEmailSender
│       ├── Sap/                       # IProviderDirectory, ISapPaymentsGateway, ISapDocumentGateway (+ Models/)
│       └── Files/                     # IFileStorage, IPdfMerger
├── UseCases/<Feature>/                # Implementaciones internas (LoginService…) y sus ayudantes
├── Contracts/<Feature>/
│   ├── Commands/                      # Entrada de los casos de uso (sin atributos HTTP)
│   └── Responses/                     # Salida de los casos de uso
├── Common/
│   ├── Exceptions/                    # AppException, ValidationException, AccountLockedException…
│   ├── Settings/                      # PortalSettings, LoginLockoutSettings
│   └── Security/                      # SessionClaims (contrato compartido por Api e Infrastructure)
└── DependencyInjection.cs

WebProveedores.Infrastructure/         # Adaptadores de salida (implementan Ports/Outbound)
├── Persistence/                       # AppDbContext, Configurations/, Repositories/, Seeding/, Migrations/
├── Security/                          # JwtTokenIssuer, IdentityPasswordHasher, JwtSettings
├── Email/                             # SmtpEmailSender, RedirectingEmailSender, LogEmailSender, EmailSettings, EmailMode
├── Sap/                               # SapProviderClient, SapPaymentsClient, MockSapDocumentGateway, Dtos/
├── Files/                             # LocalFileStorage, PdfSharpMerger
└── DependencyInjection.cs

WebProveedores.Api/                    # Adaptador de entrada HTTP + raíz de composición
├── Controllers/                       # Un controller por archivo
├── Contracts/<Feature>/               # Requests HTTP con [Required]… y su mapeo a Commands
├── Security/                          # ICurrentUser, HttpCurrentUser, Policies, RateLimitPolicies
├── Errors/                            # GlobalExceptionHandler
└── Program.cs                         # Único lugar de la Api que referencia Infrastructure

WebProveedores.Tests/
├── Architecture/                      # Reglas de dependencia (NetArchTest)
├── Unit/<Feature>/
└── Integration/
```

**Reglas que quedan fijas (y que verifican las pruebas de arquitectura):**

1. `Domain` no depende de nada del proyecto.
2. `Application` depende solo de `Domain` (y de abstracciones `Microsoft.Extensions.*.Abstractions`).
3. `Api` solo usa `Infrastructure` en `Program.cs`. Controllers y `Security/` dependen únicamente de `Ports/Inbound` y `Contracts`.
4. Cada clase de `Infrastructure` implementa un puerto de `Ports/Outbound` (o es configuración o detalle interno del adaptador).
5. Las implementaciones de `UseCases/` son `internal`; desde fuera solo se ven sus puertos.
6. Un tipo de primer nivel por archivo, con el mismo nombre que el archivo.

## 3. Pasos

Al terminar cada paso: `dotnet build WebProveedores.slnx`, `dotnet test WebProveedores.slnx` y `dotnet format whitespace --folder`, y luego un commit.

- [x] **Paso 0. Línea base.** Rama `refactor/hexagonal-v2` (`refactor/hexagonal` es un intento anterior que quedó desfasado de `main`; no se toca). Build y 91 pruebas en verde. Eliminados `WeatherForecast*` y el `.http` de plantilla (P9).
- [x] **Paso 1. Pruebas de arquitectura.** `NetArchTest.Rules` 1.3.2 en `WebProveedores.Tests/Architecture/LayerDependencyTests.cs`, con las reglas 1 y 2. Las demás reglas se activan en el paso que las haga cumplir. (P11)
- [x] **Paso 2. Dominio.** 32 tipos, uno por archivo, en `Common/Identity/Organization/Access/Documents` con namespaces nuevos. El record anidado `MenuCatalog.Entry` pasa a `MenuCatalogEntry` (P1, P8). Snapshot de EF regenerado con los nombres CLR nuevos. Verificado: la migración de prueba salió vacía y `has-pending-model-changes` no detecta cambios.
- [x] **Paso 3. Excepciones.** Las 8 excepciones de `Application` en `Common/Exceptions/`, una por archivo, incluida `ServiceUnavailableException` (P6). Nueva regla de arquitectura: toda excepción de `Application` vive en ese namespace. De paso se quitaron los usings sin uso que dejó el paso 2.
- [x] **Paso 4. Puertos de salida.** `Abstractions/` reemplazado por `Ports/Outbound/<Persistence|Security|Notifications|Sap|Files>/`: 15 interfaces y 18 modelos, uno por archivo, en `Models/` (P2, P4). `DocumentInbox` queda en `Persistence/Models`; en el paso 7 se evalúa si pasa a `Contracts`. Nueva regla de arquitectura: en `Ports/Outbound` solo hay interfaces, y sus datos van en `*.Models`. Corregidas las referencias a `Abstractions` en `CLAUDE.md` y `AGENT_CONTEXT.md`.
- [x] **Paso 5. Segregar interfaces (ISP).** Un adaptador EF por puerto, todos sobre el mismo `AppDbContext` scoped (P5):
  - `IUserRepository` (14 métodos) → `IUserRepository` (con seguimiento: 5), `IUserQueries` (sin seguimiento: 4) e `IUserUniquenessChecker` (5).
  - `IDocumentRepository` (9) → `IDocumentRepository` (3), `IDocumentSearch`, `IApproverDirectory` e `ICompanyReader`. Su `SaveChangesAsync` pasa a `IUnitOfWork`; `EfUnitOfWork` traduce ahora el índice único de documentos a `DocumentRejectedException`.
  - `IAccessRepository` → `IPermissionReader` + `IAccessRepository`. `IOrganizationRepository` → `IOrganizationReader` + `IOrganizationRepository`.
  - `IReferenceDataReader` → `IRoleReader`. Se eliminan `FindAreaAsync` y `ListActiveAreasAsync`, que no tenían uso. Las sociedades activas se unifican en `ICompanyReader.ListActiveAsync`, con seguimiento (decisión tomada).
  - Nueva regla de arquitectura: cada adaptador implementa un solo puerto de salida. `TestServices` arma `DocumentAccess` y `ProfileService` en un solo lugar.
- [x] **Paso 6. Puertos de entrada.** Las 14 interfaces de casos de uso en `Ports/Inbound/<Feature>/`, una por archivo. Implementaciones y ayudantes en `UseCases/<Feature>/`, todos `internal` (`LoginService`, `PasswordService`, `ProviderRegistrationService`, `PasswordLinks`, `EmailVerifications`, `UblDocumentReader`… eran `public`). `ElectronicDocument` y `ElectronicDocumentLine` salen de `UblDocumentReader.cs`. `PasswordPolicy` pasa a `Domain/Identity`: es una regla de dominio y la usa el seeder (P3, P4). Reglas activas: en `Ports/Inbound` solo hay interfaces, y nada en `UseCases` es público (regla 5). La regla 6 (un tipo por archivo) pasa al paso 7, porque los `*Contracts.cs` siguen agrupados hasta entonces.
- [x] **Paso 7. Contratos.** 67 DTOs, uno por archivo, en `Contracts/<Feature>/Requests|Commands|Responses`. `Requests` agrupa las clases con atributos de validación, que pasan a la Api en el paso 10. `PortalSettings` y `LoginLockoutSettings` van a `Common/Settings`. `DocumentInbox` pasa a `Domain/Documents`, para que los puertos de entrada y de salida no dependan uno del otro (decisión tomada). `CompanyScope` deja de estar anidado en `PaymentQueryService` (P1). Regla 6 activa en `Domain` y `Application` (`SourceLayoutTests`): un tipo por archivo, con su nombre, y sin tipos anidados.
- [x] **Paso 8. Adaptadores de salida.** `Infrastructure` queda en `Persistence/{Repositories,Seeding,Configurations/<contexto>}`, `Security` (antes `Auth`), `Email`, `Sap` (antes `Providers` y parte de `Documents`) y `Files`. Las 5 filas JSON de `SapPaymentsClient` pasan a `Sap/Dtos` y las 4 clases del seed a `Seeding/Models`, todas `internal`. `EmailMode` y `RoleMenuConfiguration` quedan en archivos propios. Reglas activas: los adaptadores implementan un puerto de salida (regla 4), y la regla 6 también en `Infrastructure`. Verificado: `has-pending-model-changes` sin cambios y la API arranca con el seed.
- [ ] **Paso 9. Adaptador de entrada.** `Api/Infrastructure` → `Api/Security` + `Api/Errors`. Separar `NavigationController` de `AccessController.cs` y los dos forms de `DocumentForms.cs`. Mover `SessionClaims` a `Application/Common/Security` para que `HttpCurrentUser` no dependa de `Infrastructure` (P7). Activar la regla 3.
- [ ] **Paso 10. Requests HTTP fuera del core (hexagonal estricto).** Los requests con atributos de validación pasan a `Api/Contracts/<Feature>/`. Los casos de uso reciben `Commands` sin atributos, y la validación de negocio queda en el caso de uso (P10). Es el paso más grande: se hace feature por feature (Auth → Profile → Admin → Access → Organization → Documents → Payments), con un commit cada uno.
- [ ] **Paso 11. Documentación.** Actualizar `docs/AGENT_CONTEXT.md`, `CLAUDE.md` y `README.md` con la nueva estructura y las reglas.

## 4. Decisiones tomadas

- **Nombres de los puertos de entrada:** se mantienen como `I*Service`, así los controllers no cambian.
- **Infrastructure:** un solo proyecto organizado en carpetas por adaptador. Separarlo en proyectos después es mecánico si hiciera falta.
- **`ILogger<T>` en `Application`:** se mantiene. Es una abstracción de `Microsoft.Extensions.Logging.Abstractions` y no ata el core a ningún adaptador.
