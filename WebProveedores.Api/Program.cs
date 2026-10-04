using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Application.Abstractions.Providers;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Auth;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Providers;
using WebProveedores.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddScoped<IAppDbContext>(services => services.GetRequiredService<AppDbContext>());
builder.Services.AddHttpClient<SapProviderClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IProviderDirectory>(services => services.GetRequiredService<SapProviderClient>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOnlineRegistrationService, OnlineRegistrationService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<AdminBootstrapper>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey no está configurado.");
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"], ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) };
});
builder.Services.AddAuthorization(options => options.AddPolicy("Users.Manage", policy => policy.RequireRole(SecurityCatalog.AdministratorRole)));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
