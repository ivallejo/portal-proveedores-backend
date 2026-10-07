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

    private static void AssertSuccessful(TestResult result) =>
        Assert.True(result.IsSuccessful, "Tipos que rompen la regla: " + string.Join(", ", result.FailingTypeNames ?? []));
}
