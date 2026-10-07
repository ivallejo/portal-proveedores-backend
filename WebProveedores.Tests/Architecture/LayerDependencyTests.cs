using System.Reflection;
using NetArchTest.Rules;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Common;

namespace WebProveedores.Tests.Architecture;

/// <summary>Reglas de dependencia entre capas (docs/PLAN_HEXAGONAL.md, sección 2).</summary>
public sealed class LayerDependencyTests
{
    private const string Application = "WebProveedores.Application";
    private const string Infrastructure = "WebProveedores.Infrastructure";
    private const string Api = "WebProveedores.Api";

    private static readonly Assembly DomainAssembly = typeof(DomainRuleException).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(AppException).Assembly;

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

    private static void AssertSuccessful(TestResult result) =>
        Assert.True(result.IsSuccessful, "Tipos que rompen la regla: " + string.Join(", ", result.FailingTypeNames ?? []));
}
