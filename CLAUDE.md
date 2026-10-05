# Portal de Proveedores · Backend

Leer primero [`docs/AGENT_CONTEXT.md`](docs/AGENT_CONTEXT.md): arquitectura por capas, seguridad, módulo de documentos y configuración.

- .NET 10, EF Core 10, SQL Server en Docker. Hexagonal: `Application` no depende de EF Core, ASP.NET, JWT ni `IConfiguration`; usar los puertos de `Application/Abstractions` y registrar adaptadores en `AddInfrastructure`.
- Un servicio por caso de uso; los controladores obtienen el usuario de `ICurrentUser`.
- Reglas de estado de los documentos en `Domain/Documents/SupplierDocument.cs`; no cambiar estados desde los controllers.
- Claves Guid generadas en el dominio: configurar `ValueGeneratedNever()` en entidades hijas nuevas (si no, EF las trata como existentes).
- Probar consultas nuevas contra SQL Server real: agregar un caso en `WebProveedores.Tests/Integration` (Testcontainers, requiere Docker); el proveedor InMemory acepta LINQ que SQL Server no traduce.
- No versionar `.env`, secretos ni `App_Data/`.
- Antes de terminar: `dotnet build WebProveedores.slnx`, `dotnet test WebProveedores.slnx` y `dotnet format whitespace --folder`.
