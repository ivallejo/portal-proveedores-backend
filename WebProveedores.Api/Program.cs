using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application;
using WebProveedores.Application.Common.Settings;
using WebProveedores.Infrastructure;
using WebProveedores.Infrastructure.Auth;
using WebProveedores.Infrastructure.Documents;
using WebProveedores.Infrastructure.Email;
using WebProveedores.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication(
    new PortalSettings(builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:4200"),
    new LoginLockoutSettings(builder.Configuration.GetValue("Security:MaxFailedLogins", 5), builder.Configuration.GetValue("Security:LockoutMinutes", 15)));
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsProduction());
var jwt = JwtSettings.From(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) };
});
// Cada endpoint pide la opción de menú de su pantalla (Configuración › Roles y permisos).
builder.Services.AddAuthorization(options => options.AddMenuPolicies());
builder.Services.AddScoped<IAuthorizationHandler, MenuPermissionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Límite de peticiones por IP contra fuerza bruta y abuso de los endpoints públicos.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Demasiadas solicitudes. Espera un momento antes de volver a intentarlo." }, cancellationToken);
    };
    string Client(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    options.AddPolicy(RateLimitPolicies.Login, http => RateLimitPartition.GetFixedWindowLimiter(Client(http), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 10),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    }));
    options.AddPolicy(RateLimitPolicies.Sensitive, http => RateLimitPartition.GetFixedWindowLimiter(Client(http), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue("RateLimiting:SensitivePerMinute", 10),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    }));
});

// Detrás de un proxy inverso la IP real llega en X-Forwarded-For; activar solo si el proxy es de confianza.
if (builder.Configuration.GetValue("Security:UseForwardedHeaders", false))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();
app.Logger.LogInformation("Correo: modo {EmailMode}", app.Services.GetRequiredService<EmailSettings>().Describe());
if (app.Services.GetRequiredService<SapDocumentSettings>().Mode == SapDocumentSettings.Simulated)
    app.Logger.LogWarning("SAP 01/02 (orden, SUNAT y duplicidad): SIMULADOS. Las órdenes válidas son las de prueba.");

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue("Security:UseForwardedHeaders", false)) app.UseForwardedHeaders();

if (app.Configuration.GetValue("Security:RequireHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // Las respuestas de la API llevan datos de sesión o de documentos: no deben quedar en cachés.
    if (context.Request.Path.StartsWithSegments("/api")) headers.CacheControl = "no-store";
    await next();
});

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();

// Con contraseña temporal la sesión solo sirve para cambiarla (y consultar el propio usuario).
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (context.User.HasClaim(SessionClaims.PasswordChangeOnly, "1")
        && path.StartsWithSegments("/api")
        && !path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
        && !path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "PASSWORD_CHANGE_REQUIRED", message = "Debes cambiar tu contraseña temporal antes de continuar." });
        return;
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
