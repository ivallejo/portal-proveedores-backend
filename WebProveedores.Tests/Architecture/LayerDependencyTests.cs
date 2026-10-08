using System.Reflection;
using NetArchTest.Rules;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Common;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests.Architecture;

/// <summary>Reglas de dependencia entre capas (docs/PLAN_HEXAGONAL.md, sección 2).</summary>
public sealed class LayerDependencyTests
{
    private const string Application = "WebProveedores.Application";
    private const string Infrastructure = "WebProveedores.Infrastructure";
    private const string Api = "WebProveedores.Api";

    private static readonly Assembly DomainAssembly = typeof(DomainRuleException).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(AppException).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api, "Microsoft")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_depends_only_on_domain_and_extension_abstractions()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                Infrastructure,
                Api,
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions.Configuration",
                "Microsoft.Extensions.Http",
                "System.IdentityModel",
                "Microsoft.IdentityModel")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_exceptions_live_in_common_exceptions()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .Inherit(typeof(Exception))
            .Should()
            .ResideInNamespace("WebProveedores.Application.Common.Exceptions")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Outbound_ports_are_interfaces_and_their_data_lives_in_models()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("WebProveedores.Application.Ports.Outbound")
            .And()
            .AreNotInterfaces()
            .Should()
            .ResideInNamespaceMatching(@"^WebProveedores\.Application\.Ports\.Outbound\.\w+\.Models$")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Inbound_ports_are_interfaces()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("WebProveedores.Application.Ports.Inbound")
            .Should()
            .BeInterfaces()
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Use_cases_are_only_reachable_through_their_ports()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("WebProveedores.Application.UseCases")
            .ShouldNot()
            .BePublic()
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Each_adapter_implements_a_single_outbound_port()
    {
        const string outbound = "WebProveedores.Application.Ports.Outbound";
        var offenders = InfrastructureAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => (type.Name, Ports: type.GetInterfaces().Count(port => port.Namespace?.StartsWith(outbound, StringComparison.Ordinal) == true)))
            .Where(item => item.Ports > 1)
            .Select(item => item.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Adaptadores con más de un puerto: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Adapters_implement_an_outbound_port()
    {
        // Lo demás en Infrastructure es configuración (Settings), formato de un servicio externo (Dtos), modelo del seed
        // o detalle de EF (contexto, configuraciones, migraciones, inicialización).
        string[] adapterNamespaces =
            ["WebProveedores.Infrastructure.Persistence.Repositories", "WebProveedores.Infrastructure.Security",
             "WebProveedores.Infrastructure.Email", "WebProveedores.Infrastructure.Sap", "WebProveedores.Infrastructure.Files"];
        const string outbound = "WebProveedores.Application.Ports.Outbound";
        var offenders = InfrastructureAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false } && adapterNamespaces.Contains(type.Namespace))
            .Where(type => !type.Name.EndsWith("Settings", StringComparison.Ordinal))
            .Where(type => !type.GetInterfaces().Any(port => port.Namespace?.StartsWith(outbound, StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Clases de adaptadores que no implementan un puerto de salida: " + string.Join(", ", offenders));
    }

    private static void AssertSuccessful(TestResult result) =>
        Assert.True(result.IsSuccessful, "Tipos que rompen la regla: " + string.Join(", ", result.FailingTypeNames ?? []));
}
