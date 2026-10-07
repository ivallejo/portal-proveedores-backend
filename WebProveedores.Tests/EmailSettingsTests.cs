using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Infrastructure.Email;

namespace WebProveedores.Tests;

public sealed class EmailSettingsTests
{
    private static readonly Dictionary<string, string?> Smtp = new()
    {
        ["Smtp:Host"] = "smtp.gmail.com",
        ["Smtp:Username"] = "portal@ejemplo.test",
        ["Smtp:Password"] = "app-password",
    };

    [Fact]
    public void Defaults_to_send_in_production_and_redirect_elsewhere()
    {
        Assert.Equal(EmailMode.Send, EmailSettings.Resolve(Config(), isProduction: true).Mode);

        var development = EmailSettings.Resolve(Config(("Email:TestRecipient", "pruebas@ejemplo.test")), isProduction: false);
        Assert.Equal(EmailMode.Redirect, development.Mode);
        Assert.Equal("pruebas@ejemplo.test", development.TestRecipient);

        // Sin buzón de pruebas, fuera de producción no envía nada.
        Assert.Equal(EmailMode.Log, EmailSettings.Resolve(Config(), isProduction: false).Mode);
    }

    [Fact]
    public void Send_outside_production_requires_explicit_permission()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EmailSettings.Resolve(Config(("Email:Mode", "Send")), isProduction: false));
        Assert.Contains("proveedores reales", exception.Message);

        var allowed = EmailSettings.Resolve(Config(("Email:Mode", "Send"), ("Email:AllowSendOutsideProduction", "true")), isProduction: false);
        Assert.Equal(EmailMode.Send, allowed.Mode);
    }

    [Theory]
    [InlineData("Redirect", null, "TestRecipient")]
    [InlineData("Redirect", "no-es-correo", "TestRecipient")]
    [InlineData("Enviar", null, "no es válido")]
    public void Invalid_configuration_stops_the_startup(string mode, string? recipient, string expected)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmailSettings.Resolve(Config(("Email:Mode", mode), ("Email:TestRecipient", recipient)), isProduction: false));
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void Send_and_redirect_need_the_smtp_server()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Mode"] = "Send" }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() => EmailSettings.Resolve(configuration, isProduction: true));
        Assert.Contains("Smtp:Password", exception.Message);
    }

    [Fact]
    public void Legacy_smtp_redirect_flag_is_still_honored()
    {
        var settings = EmailSettings.Resolve(Config(("Smtp:RedirectEnabled", "true"), ("Smtp:TestRecipient", "pruebas@ejemplo.test")), isProduction: true);

        Assert.Equal(EmailMode.Redirect, settings.Mode);
        Assert.Equal("pruebas@ejemplo.test", settings.TestRecipient);
    }

    [Fact]
    public async Task Redirect_only_writes_to_the_test_mailbox()
    {
        var inner = new CapturingSender();

        await new RedirectingEmailSender(inner, "pruebas@ejemplo.test").SendAsync("proveedor@real.pe", "Completa tu registro", "<p>Hola</p>", CancellationToken.None, isHtml: true);

        Assert.Equal("pruebas@ejemplo.test", inner.Recipient);
        Assert.Equal("[PRUEBA] Completa tu registro", inner.Subject);
        Assert.Contains("proveedor@real.pe", inner.Body);
        Assert.EndsWith("<p>Hola</p>", inner.Body);
    }

    [Fact]
    public async Task Redirect_does_not_send_copies_but_reports_them()
    {
        var inner = new CapturingSender();

        await new RedirectingEmailSender(inner, "pruebas@ejemplo.test").SendAsync(
            "proveedor@real.pe", "Documento observado", "<p>Hola</p>", CancellationToken.None, isHtml: true, copyTo: ["facturacion@sociedad.pe"]);

        Assert.Null(inner.CopyTo);
        Assert.Contains("copia: facturacion@sociedad.pe", inner.Body);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values)
    {
        var settings = new Dictionary<string, string?>(Smtp);
        foreach (var (key, value) in values) settings[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class CapturingSender : IEmailSender
    {
        public string? Recipient { get; private set; }
        public string? Subject { get; private set; }
        public string? Body { get; private set; }
        public IReadOnlyList<string>? CopyTo { get; private set; }

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null)
        {
            (Recipient, Subject, Body, CopyTo) = (recipient, subject, body, copyTo);
            return Task.CompletedTask;
        }
    }
}
