using Microsoft.Extensions.Configuration;
using WebProveedores.Infrastructure.Documents;

namespace WebProveedores.Tests;

public sealed class SapDocumentSettingsTests
{
    [Fact]
    public void Simulator_is_the_default_only_outside_production()
    {
        Assert.Equal(SapDocumentSettings.Simulated, SapDocumentSettings.Resolve(Config(null), isProduction: false).Mode);
        Assert.Throws<InvalidOperationException>(() => SapDocumentSettings.Resolve(Config(null), isProduction: true));
        Assert.Equal(SapDocumentSettings.Simulated, SapDocumentSettings.Resolve(Config("Simulated"), isProduction: true).Mode);
        Assert.Throws<InvalidOperationException>(() => SapDocumentSettings.Resolve(Config("Real"), isProduction: false));
    }

    private static IConfiguration Config(string? mode) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sap:DocumentServices"] = mode }).Build();
}
