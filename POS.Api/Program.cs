using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using POS.Infrastructure.Marten;

var builder = WebApplication.CreateBuilder(args);

// HttpContextAccessor para auditoría
builder.Services.AddHttpContextAccessor();

// Entity Framework Core (PostgreSQL - schema public)
builder.Services.AddAppDbContext(builder.Configuration);

// Services (ANTES de Marten - las projections usan BuildServiceProvider y necesitan estos servicios)
builder.Services.AddScoped<POS.Application.Services.ITerceroService, POS.Infrastructure.Services.TerceroLocalService>();
builder.Services.AddScoped<POS.Application.Services.IProductoService, POS.Infrastructure.Services.ProductoLocalService>();
builder.Services.AddScoped<POS.Infrastructure.Services.CosteoService>();
builder.Services.AddScoped<POS.Infrastructure.Services.PrecioService>();
builder.Services.AddScoped<POS.Infrastructure.Services.UsuarioService>();

// Activity Log Service (Singleton para Channel-based background processing)
builder.Services.AddSingleton<POS.Application.Services.IActivityLogService, POS.Infrastructure.Services.ActivityLogService>();

// Marten Event Store (PostgreSQL - schema events)
builder.Services.AddMartenStore(
    builder.Configuration,
    builder.Environment.IsDevelopment());

// API
builder.Services.AddControllers(options =>
{
    // En desarrollo: ignorar autenticación/autorización
    if (builder.Environment.IsDevelopment())
    {
        options.Filters.Add<POS.Api.Filters.AllowAnonymousFilter>();
    }
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<POS.Application.Validators.CrearProductoValidator>();

// Authentication & Authorization
if (builder.Environment.IsDevelopment())
{
    // En desarrollo: esquema de autenticación que permite todo
    builder.Services.AddAuthentication("DevScheme")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, POS.Api.Auth.DevAuthenticationHandler>("DevScheme", null);

    builder.Services.AddAuthorization(options =>
    {
        // En desarrollo: permitir todo
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();

        // Definir las políticas para que no falle
        options.AddPolicy("Admin", policy => policy.RequireAssertion(_ => true));
        options.AddPolicy("Supervisor", policy => policy.RequireAssertion(_ => true));
        options.AddPolicy("Cajero", policy => policy.RequireAssertion(_ => true));
        options.AddPolicy("Vendedor", policy => policy.RequireAssertion(_ => true));
    });
}
else
{
    // PRODUCCION: Azure AD B2C
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authConfig = builder.Configuration.GetSection("Authentication");
        options.Authority = authConfig["Authority"];
        options.Audience = authConfig["Audience"];
        options.RequireHttpsMetadata = authConfig.GetValue<bool>("RequireHttpsMetadata");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = authConfig.GetValue<bool>("ValidateIssuer"),
            ValidateAudience = authConfig.GetValue<bool>("ValidateAudience"),
            ValidateLifetime = authConfig.GetValue<bool>("ValidateLifetime"),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            RoleClaimType = ClaimTypes.Role  // Usar ClaimTypes.Role ya que mapeamos manualmente en OnTokenValidated
        };

        // Eventos para debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                // Mapear roles de Keycloak a claims de .NET
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    // Buscar el claim que contiene realm_access como JSON
                    var allClaims = context.Principal.Claims.ToList();

                    // Intentar diferentes nombres de claim que Keycloak podría usar
                    var realmAccessClaim = allClaims.FirstOrDefault(c =>
                        c.Type == "realm_access" ||
                        c.Type.EndsWith("/realm_access"));

                    if (realmAccessClaim != null && !string.IsNullOrEmpty(realmAccessClaim.Value))
                    {
                        try
                        {
                            var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                            if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                            {
                                foreach (var role in rolesElement.EnumerateArray())
                                {
                                    var roleName = role.GetString();
                                    if (!string.IsNullOrEmpty(roleName))
                                    {
                                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                                        logger.LogInformation("Added role: {Role}", roleName);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to parse realm_access claim");
                        }
                    }
                    else
                    {
                        logger.LogWarning("realm_access claim not found. Available claims: {Claims}",
                            string.Join(", ", allClaims.Select(c => c.Type)));
                    }
                }

                var claims = context.Principal?.Claims.Select(c => $"{c.Type}: {c.Value}");
                logger.LogInformation("Token validated. Final claims: {Claims}", string.Join(", ", claims ?? Array.Empty<string>()));
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        // Políticas basadas en roles (Azure AD B2C en producción)
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
        options.AddPolicy("Supervisor", policy => policy.RequireRole("admin", "supervisor"));
        options.AddPolicy("Cajero", policy => policy.RequireRole("admin", "supervisor", "cajero"));
        options.AddPolicy("Vendedor", policy => policy.RequireRole("admin", "supervisor", "cajero", "vendedor"));
    });
}

// CORS - RESTRINGIR EN PRODUCCION
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}

app.UseHttpsRedirection();
// DESARROLLO: autenticación/autorización permisiva
// PRODUCCION: Azure AD B2C
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hacer Program accesible para tests de integracion
public partial class Program { }
