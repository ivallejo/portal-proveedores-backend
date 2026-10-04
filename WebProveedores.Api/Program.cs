using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Application.Abstractions.Providers;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Documents;
using WebProveedores.Api.Controllers;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Auth;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Providers;
using WebProveedores.Infrastructure.Documents;
using WebProveedores.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddScoped<IIdentityRepository, EfIdentityRepository>();
builder.Services.AddHttpClient<SapProviderClient>(client => client.Timeout = TimeSpan.FromSeconds(15))
    // Reintentos con espera, corte de circuito y tiempos máximos ante caídas de SAP.
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 2;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
    });
builder.Services.AddScoped<IProviderDirectory>(services => services.GetRequiredService<SapProviderClient>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOnlineRegistrationService, OnlineRegistrationService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<AdminBootstrapper>();
builder.Services.AddScoped<ReferenceDataSeeder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IPdfMerger, PdfSharpMerger>();
// Servicios SAP 01/02 simulados hasta contar con los endpoints reales.
builder.Services.AddSingleton<ISapDocumentGateway, MockSapDocumentGateway>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey no está configurado.");
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"], ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.UsersManage, policy => policy.RequireRole(SecurityCatalog.AdministratorRole));
    options.AddPolicy(Policies.DocumentsRegister, policy => policy.RequireRole(SecurityCatalog.ProviderRole, SecurityCatalog.InternalUserRole, SecurityCatalog.AdministratorRole));
    options.AddPolicy(Policies.DocumentsApprove, policy => policy.RequireRole(SecurityCatalog.AreaApproverRole, SecurityCatalog.AdministratorRole));
    options.AddPolicy(Policies.DocumentsAccount, policy => policy.RequireRole(SecurityCatalog.AccountsPayableRole, SecurityCatalog.AdministratorRole));
});
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
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
