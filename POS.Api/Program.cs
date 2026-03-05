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
builder.Services.AddScoped<POS.Infrastructure.Services.MigracionLogService>();
builder.Services.AddScoped<POS.Infrastructure.Services.ITaxEngine, POS.Infrastructure.Services.TaxEngine>();
builder.Services.AddScoped<POS.Application.Services.IVentaService, POS.Infrastructure.Services.VentaService>();
builder.Services.AddScoped<POS.Application.Services.ICompraService, POS.Infrastructure.Services.CompraService>();
builder.Services.AddScoped<POS.Application.Services.ITrasladoService, POS.Infrastructure.Services.TrasladoService>();
builder.Services.AddScoped<POS.Application.Services.IInventarioService, POS.Infrastructure.Services.InventarioService>();

// GeoService (Países y Ciudades)
builder.Services.AddHttpClient<POS.Infrastructure.Services.GeoService>();
builder.Services.AddMemoryCache();

// Activity Log Service (Singleton para Channel-based background processing)
builder.Services.AddSingleton<POS.Application.Services.IActivityLogService, POS.Infrastructure.Services.ActivityLogService>();

// Marten Event Store (PostgreSQL - schema events)
builder.Services.AddMartenStore(
    builder.Configuration,
    builder.Environment.IsDevelopment());

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SincoPos API",
        Version = "v1",
        Description = """
            API del sistema de Punto de Venta SincoPos.

            **Roles de acceso** (de mayor a menor permiso):
            - `Admin` → acceso total
            - `Supervisor` → gestión de inventario, compras, traslados, devoluciones
            - `Cajero` → ventas, consultas
            - `Vendedor` → solo lectura de productos y precios

            Autenticación vía **Keycloak** (JWT Bearer). Realm: `sincopos`.
            """
    });

    // XML comments generados por el compilador
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // JWT security definition
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Token JWT de Keycloak. Obtener en http://localhost:8080/realms/sincopos"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<POS.Application.Validators.CrearProductoValidator>();

// Authentication & Authorization — Keycloak JWT Bearer
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
        RoleClaimType = ClaimTypes.Role
    };

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

            // Mapear roles de Keycloak a ClaimTypes.Role de .NET
            // Keycloak puede enviar roles de dos formas según el JWT handler:
            //   1. JwtSecurityTokenHandler: claim "realm_access" con JSON string {"roles":["admin",...]}
            //   2. JsonWebTokenHandler (.NET 8+): claims directos "realm_access.roles" = "admin"
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var allClaims = context.Principal.Claims.ToList();
                var rolesAdded = 0;

                // Formato 1: realm_access como JSON string (JwtSecurityTokenHandler)
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
                                    rolesAdded++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse realm_access JSON claim");
                    }
                }

                // Formato 2: realm_access.roles como claims directos (JsonWebTokenHandler / .NET 9+)
                var directRoleClaims = allClaims
                    .Where(c => c.Type == "realm_access.roles")
                    .ToList();

                foreach (var roleClaim in directRoleClaims)
                {
                    if (!string.IsNullOrEmpty(roleClaim.Value))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                        rolesAdded++;
                    }
                }

                if (rolesAdded == 0)
                {
                    logger.LogWarning(
                        "No roles found in Keycloak token. Available claim types: {Claims}",
                        string.Join(", ", allClaims.Select(c => c.Type).Distinct()));
                }
                else
                {
                    logger.LogInformation("Mapped {Count} roles from Keycloak token", rolesAdded);
                }
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("Supervisor", policy => policy.RequireRole("admin", "supervisor"));
    options.AddPolicy("Cajero", policy => policy.RequireRole("admin", "supervisor", "cajero"));
    options.AddPolicy("Vendedor", policy => policy.RequireRole("admin", "supervisor", "cajero", "vendedor"));
});

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
