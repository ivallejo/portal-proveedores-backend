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
- [ ] **Paso 1. Pruebas de arquitectura.** Agregar `NetArchTest.Rules` a `WebProveedores.Tests` con las reglas 1 y 2, que ya se cumplen hoy. Las demás reglas se activan en el paso que las haga cumplir. (P11)
- [ ] **Paso 2. Dominio.** Separar en un tipo por archivo: enums de `DocumentEnums.cs`, tipos de `SupplierDocument.cs`, `MenuOption.cs`, `AppUser.cs`, `UserEmail.cs` y `PasswordResetToken.cs`. Reubicar en `Common/Identity/Organization/Access/Documents`, con namespaces nuevos (P1, P8). Riesgo: el snapshot de migraciones guarda nombres CLR. Verificación: `dotnet ef migrations add VerificaNamespaces` debe salir vacía; después se borra.
- [ ] **Paso 3. Excepciones.** Llevar `Errors.cs`, `AccountLockedException` y `DocumentRejectedException` a `Application/Common/Exceptions/`, una por archivo (P6). Actualizar `GlobalExceptionHandler`.
- [ ] **Paso 4. Puertos de salida.** Mover `Abstractions/` a `Ports/Outbound/<Persistence|Security|Notifications|Sap|Files>/`. Sacar cada record a `Models/`, uno por archivo. Mover `ServiceUnavailableException` a `Common/Exceptions` (P2, P4).
- [ ] **Paso 5. Segregar interfaces (ISP).** Revisar qué métodos usa cada caso de uso y dividir:
  - `IUserRepository` → `IUserReader` (consultas sin seguimiento), `IUserWriter` (`FindTrackedByIdAsync`, `Add`) e `IUserUniquenessChecker` (`*ExistsAsync`, `EmailUsedByOtherAsync`).
  - `IDocumentRepository` → `ICompanyCatalogReader`, `IApproverDirectory` e `IDocumentRepository`. Se quita `SaveChangesAsync` y se usa `IUnitOfWork`.
  - Revisar con el mismo criterio `IAccessRepository`, `IOrganizationRepository` y `IReferenceDataReader`.
  - Un adaptador EF puede implementar varias interfaces. Lo que cambia es lo que ve cada consumidor (P5).
- [ ] **Paso 6. Puertos de entrada.** Sacar las interfaces de casos de uso de `*Contracts.cs` y `DocumentServices.cs` a `Ports/Inbound/<Feature>/`, una por archivo. Mover implementaciones y ayudantes (`DocumentAccess`, `DocumentFiles`, `PasswordLinks`, `UblDocumentReader`…) a `UseCases/<Feature>/` (P3, P4). Activar las reglas 5 y 6.
- [ ] **Paso 7. Contratos.** Repartir los DTOs restantes en `Contracts/<Feature>/Commands|Responses`, uno por archivo. `PortalSettings` y `LoginLockoutSettings` van a `Common/Settings` (P1).
- [ ] **Paso 8. Adaptadores de salida.** Reorganizar `Infrastructure` según la estructura objetivo: repositorios en `Persistence/Repositories`, seeder en `Persistence/Seeding`, `Providers` → `Sap`, `Documents` → `Files` + `Sap`. Un tipo por archivo (`SapPaymentsClient`, `ReferenceDataSeeder`, `EmailSettings`, `MenuOptionConfiguration`). Activar la regla 4.
- [ ] **Paso 9. Adaptador de entrada.** `Api/Infrastructure` → `Api/Security` + `Api/Errors`. Separar `NavigationController` de `AccessController.cs` y los dos forms de `DocumentForms.cs`. Mover `SessionClaims` a `Application/Common/Security` para que `HttpCurrentUser` no dependa de `Infrastructure` (P7). Activar la regla 3.
- [ ] **Paso 10. Requests HTTP fuera del core (hexagonal estricto).** Los requests con atributos de validación pasan a `Api/Contracts/<Feature>/`. Los casos de uso reciben `Commands` sin atributos, y la validación de negocio queda en el caso de uso (P10). Es el paso más grande: se hace feature por feature (Auth → Profile → Admin → Access → Organization → Documents → Payments), con un commit cada uno.
- [ ] **Paso 11. Documentación.** Actualizar `docs/AGENT_CONTEXT.md`, `CLAUDE.md` y `README.md` con la nueva estructura y las reglas.

## 4. Decisiones tomadas

- **Nombres de los puertos de entrada:** se mantienen como `I*Service`, así los controllers no cambian.
- **Infrastructure:** un solo proyecto organizado en carpetas por adaptador. Separarlo en proyectos después es mecánico si hiciera falta.
- **`ILogger<T>` en `Application`:** se mantiene. Es una abstracción de `Microsoft.Extensions.Logging.Abstractions` y no ata el core a ningún adaptador.
