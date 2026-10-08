# Portal de Proveedores · Backend

Leer primero [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md): arquitectura, «dónde va cada cosa», seguridad, módulo de documentos y configuración.

- .NET 10, EF Core 10, SQL Server en Docker. Arquitectura hexagonal estricta, verificada por `WebProveedores.Tests/Architecture`: si una regla falla, corregir el código, no la prueba.
- `Application` no depende de EF Core, ASP.NET, JWT, `IConfiguration`, `HttpClient` ni `DataAnnotations`. Caso de uso nuevo: interfaz en `Ports/Inbound/<Feature>`, implementación `internal` en `UseCases/<Feature>`, registro en `AddApplication`. Acceso externo: puerto pequeño en `Ports/Outbound/<Área>` (datos en `Models/`) y un adaptador por puerto en `Infrastructure/<Área>`, registrado en `AddInfrastructure`.
- La Api solo usa puertos de entrada (fuera de `Program.cs`, nada de `Infrastructure`). Requests HTTP con validaciones en `Api/Contracts/<Feature>`, convertidos a commands sin atributos (`Application/Contracts/<Feature>/Commands`) con `<Feature>RequestMappings.ToCommand()`. El usuario sale de `ICurrentUser`.
- Un tipo de primer nivel por archivo, con su nombre, y sin tipos anidados (records, enums y excepciones en su propio archivo).
- Reglas de estado de los documentos en `Domain/Documents/SupplierDocument.cs`; no cambiar estados desde los controllers.
- Claves Guid generadas en el dominio: configurar `ValueGeneratedNever()` en entidades hijas nuevas (si no, EF las trata como existentes).
- Probar consultas nuevas contra SQL Server real: agregar un caso en `WebProveedores.Tests/Integration` (Testcontainers, requiere Docker); el proveedor InMemory acepta LINQ que SQL Server no traduce.
- No versionar `.env`, secretos ni `App_Data/`.
- Antes de terminar: `dotnet build WebProveedores.slnx`, `dotnet test WebProveedores.slnx` y `dotnet format whitespace --folder`.
