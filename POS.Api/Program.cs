using System.IO.Compression;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<POS.Application.Services.IPrecioService, POS.Infrastructure.Services.PrecioService>();

// Identity Provider abstraction: EntraIdService (prod) / LocalIdentityProviderService (dev)
if (builder.Configuration.GetSection("MicrosoftGraph:TenantId").Exists())
{
    builder.Services.Configure<POS.Infrastructure.Configuration.MicrosoftGraphOptions>(
        builder.Configuration.GetSection(POS.Infrastructure.Configuration.MicrosoftGraphOptions.SectionName));
    builder.Services.AddScoped<POS.Application.Services.IIdentityProviderService, POS.Infrastructure.Services.EntraIdService>();
}
else
{
    builder.Services.AddScoped<POS.Application.Services.IIdentityProviderService, POS.Infrastructure.Services.LocalIdentityProviderService>();
}

// Usuario service: interface + concrete (concrete kept for backward compat with CajasController)
builder.Services.AddScoped<POS.Application.Services.IUsuarioService, POS.Infrastructure.Services.UsuarioService>();
builder.Services.AddScoped<POS.Infrastructure.Services.UsuarioService>();

builder.Services.AddScoped<POS.Infrastructure.Services.MigracionLogService>();
builder.Services.AddScoped<POS.Infrastructure.Services.ITaxEngine, POS.Infrastructure.Services.TaxEngine>();
builder.Services.AddScoped<POS.Application.Services.IVentaService, POS.Infrastructure.Services.VentaService>();
builder.Services.AddScoped<POS.Application.Services.ICompraService, POS.Infrastructure.Services.CompraService>();
builder.Services.AddScoped<POS.Application.Services.ITrasladoService, POS.Infrastructure.Services.TrasladoService>();
builder.Services.AddScoped<POS.Application.Services.IInventarioService, POS.Infrastructure.Services.InventarioService>();
builder.Services.AddScoped<POS.Application.Services.IReportesService, POS.Infrastructure.Services.ReportesService>();

// GeoService (Países y Ciudades)
builder.Services.AddHttpClient<POS.Infrastructure.Services.GeoService>();
builder.Services.AddMemoryCache();

// ── Escalabilidad: Response Compression (Brotli + Gzip) ──────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/plain", "application/xml" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.SmallestSize);

// ── Escalabilidad: Rate Limiting ─────────────────────────────────────────
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política global: N requests por ventana de tiempo por IP
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = rateLimitConfig.GetValue("PermitLimit", 100);
        opt.Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue("WindowSeconds", 60));
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = rateLimitConfig.GetValue("QueueLimit", 10);
    });

    // Política de escrituras: máx 10 concurrentes por IP
    options.AddConcurrencyLimiter("writes", opt =>
    {
        opt.PermitLimit = 10;
        opt.QueueLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Particionar por IP remota — excluir health checks y SignalR negotiate
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitConfig.GetValue("PermitLimit", 100),
                Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue("WindowSeconds", 60))
            });
    });
});

// ── Escalabilidad: Health Checks ─────────────────────────────────────────
var healthCheckConnStr = builder.Configuration.GetConnectionString("Postgres")!;
builder.Services.AddHealthChecks()
    .AddNpgSql(healthCheckConnStr, name: "postgresql", tags: new[] { "db", "ready" });

// Activity Log Service (Singleton para Channel-based background processing)
builder.Services.AddSingleton<POS.Application.Services.IActivityLogService, POS.Infrastructure.Services.ActivityLogService>();

// Facturación Electrónica DIAN
builder.Services.AddScoped<POS.Application.Services.IUblBuilderService, POS.Infrastructure.Services.UblBuilderService>();
builder.Services.AddScoped<POS.Application.Services.IFirmaDigitalService, POS.Infrastructure.Services.FirmaDigitalService>();
builder.Services.AddHttpClient<POS.Infrastructure.Services.DianSoapService>();
builder.Services.AddScoped<POS.Application.Services.IDianSoapService, POS.Infrastructure.Services.DianSoapService>();
builder.Services.AddScoped<POS.Application.Services.IFacturacionService, POS.Infrastructure.Services.FacturacionService>();
// BackgroundService Singleton (Channel fire-and-forget, igual que ActivityLog)
builder.Services.AddSingleton<POS.Infrastructure.Services.FacturacionBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<POS.Infrastructure.Services.FacturacionBackgroundService>());

// ERP Integration
builder.Services.Configure<POS.Infrastructure.Services.Erp.ErpSincoOptions>(
    builder.Configuration.GetSection(POS.Infrastructure.Services.Erp.ErpSincoOptions.SectionName));

var erpBaseUrl = builder.Configuration.GetSection("ErpSinco:BaseUrl").Value;
if (!string.IsNullOrEmpty(erpBaseUrl))
{
    // Producción: cliente real contra ERP Sinco
    builder.Services.AddHttpClient<POS.Application.Services.IErpClient, POS.Infrastructure.Services.Erp.SincoErpClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<POS.Infrastructure.Services.Erp.ErpSincoOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });
}
else
{
    // Desarrollo: cliente mock que simula respuestas sin contactar ERP externo
    builder.Services.AddSingleton<POS.Application.Services.IErpClient, POS.Infrastructure.Services.Erp.MockErpClient>();
}

builder.Services.AddHostedService<POS.Infrastructure.Services.Erp.ErpSyncBackgroundService>();

// Marten Event Store (PostgreSQL - schema events)
builder.Services.AddMartenStore(
    builder.Configuration,
    builder.Environment.IsDevelopment());

// Output Cache para catálogos (categorías, impuestos, sucursales, geografía)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("Catalogo5m", b => b
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery(Array.Empty<string>())
        .Tag("catalogo"));
    options.AddPolicy("Catalogo1h", b => b
        .Expire(TimeSpan.FromHours(1))
        .SetVaryByQuery(Array.Empty<string>())
        .Tag("catalogo"));
});

// SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<POS.Application.Services.INotificationService,
    POS.Api.Services.NotificationService>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// API Versioning
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
})
.AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";
    o.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<POS.Api.Infrastructure.ConfigureSwaggerOptions>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<POS.Application.Validators.CrearProductoValidator>();

// Authentication & Authorization — Entra ID (producción) / Keycloak (desarrollo)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    var authConfig = builder.Configuration.GetSection("Authentication");
    options.Authority = authConfig["Authority"];
    options.Audience = authConfig["Audience"];
    options.RequireHttpsMetadata = authConfig.GetValue<bool>("RequireHttpsMetadata");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false, // Temporarily disabled to diagnose 401
        ValidateLifetime = authConfig.GetValue<bool>("ValidateLifetime"),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(5),
        RoleClaimType = ClaimTypes.Role,
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) &&
                context.Request.Path.StartsWithSegments("/hubs/notificaciones"))
                context.Token = token;
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "Authentication failed: {Error}", context.Exception.Message);
            // Return error detail in response header for debugging (sanitize newlines)
            var safeMessage = context.Exception.Message.Replace("\r", "").Replace("\n", " ");
            context.Response.Headers.Append("X-Auth-Error", safeMessage);
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var allClaims = context.Principal.Claims.ToList();
                var rolesAdded = 0;

                // ── Entra ID: roles como claim "roles" (array plano) ──
                var entraRoleClaims = allClaims
                    .Where(c => c.Type == "roles")
                    .ToList();

                foreach (var roleClaim in entraRoleClaims)
                {
                    if (!string.IsNullOrEmpty(roleClaim.Value))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                        rolesAdded++;
                    }
                }

                // ── Keycloak fallback: realm_access JSON string ──
                if (rolesAdded == 0)
                {
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

                    // Keycloak: realm_access.roles como claims directos (.NET 9+)
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
                }

                // ── Fallback: buscar rol en BD si el token no tiene roles ──
                if (rolesAdded == 0)
                {
                    var externalId = context.Principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                        ?? context.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

                    if (!string.IsNullOrEmpty(externalId))
                    {
                        try
                        {
                            var dbContext = context.HttpContext.RequestServices
                                .GetRequiredService<POS.Infrastructure.Data.AppDbContext>();
                            var usuario = await dbContext.Set<POS.Infrastructure.Data.Entities.Usuario>()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.KeycloakId == externalId);

                            if (usuario != null && !string.IsNullOrEmpty(usuario.Rol))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, usuario.Rol));
                                rolesAdded++;
                                logger.LogInformation("Role '{Role}' loaded from DB for user {Email}",
                                    usuario.Rol, usuario.Email);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to load role from DB for externalId {Id}", externalId);
                        }
                    }
                }

                if (rolesAdded == 0)
                {
                    logger.LogWarning(
                        "No roles found in token or DB. Available claim types: {Claims}",
                        string.Join(", ", allClaims.Select(c => c.Type).Distinct()));
                }
                else
                {
                    logger.LogInformation("Mapped {Count} roles for authenticated user", rolesAdded);
                }
            }

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

// CORS — configurable por entorno
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
}
else
{
    // Producción: origenes desde appsettings.json o Variables de entorno de Azure
    var corsConfig = builder.Configuration.GetSection("Cors:AllowedOrigins");
    
    // Intentar leer como array (appsettings.json) o como string separado por comas (Azure App Settings)
    var allowedOrigins = corsConfig.Get<string[]>() 
        ?? builder.Configuration.GetValue<string>("Cors:AllowedOrigins")?.Split(',') 
        ?? Array.Empty<string>();
        
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Any())
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
            else
            {
                // Fallback seguro si no se configuran orígenes
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
        });
    });
}


var app = builder.Build();

// ── Sincronizar schema de BD (columnas faltantes) ───────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();
    var migrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        // Aplicar migraciones pendientes si las hay
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            migrationLogger.LogWarning("Aplicando {Count} migraciones pendientes: {Migrations}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            migrationLogger.LogWarning("Migraciones aplicadas exitosamente");
        }

        // Reparar columnas faltantes (historial de migraciones incorrecto en producción)
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$
            BEGIN
                -- unidad_medida en productos
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='productos' AND column_name='unidad_medida')
                THEN
                    ALTER TABLE public.productos ADD COLUMN unidad_medida varchar(10) NOT NULL DEFAULT '94';
                    RAISE NOTICE 'Columna unidad_medida agregada a productos';
                END IF;

                -- fecha_asignacion en usuario_sucursales
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='usuario_sucursales' AND column_name='fecha_asignacion')
                THEN
                    ALTER TABLE public.usuario_sucursales ADD COLUMN fecha_asignacion timestamp with time zone NOT NULL DEFAULT NOW();
                    RAISE NOTICE 'Columna fecha_asignacion agregada a usuario_sucursales';
                END IF;
            END $$;
        ");
    }
    catch (Exception ex)
    {
        migrationLogger.LogError(ex, "Error al sincronizar schema de BD");
    }
}

// ── Escalabilidad: Global Exception Handler (JSON consistente) ───────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (exceptionFeature != null)
        {
            logger.LogError(exceptionFeature.Error,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Ha ocurrido un error interno. Contacte soporte.",
            traceId
        });
    });
});

// ── Escalabilidad: Response Compression ──────────────────────────────────
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in app.DescribeApiVersions())
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"SincoPos API {description.GroupName.ToUpperInvariant()}");
        }
    });
}

// Azure App Service termina TLS en el load balancer — no redirigir dentro del contenedor
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors();
app.UseWebSockets();
if (!app.Environment.IsDevelopment())
    app.UseRateLimiter();
// DESARROLLO: autenticación/autorización permisiva
// PRODUCCION: Azure AD B2C
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.MapControllers();
app.MapHub<POS.Api.Hubs.NotificationHub>("/hubs/notificaciones");

// ── Escalabilidad: Health Check endpoints ────────────────────────────────
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds + "ms",
                error = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
        });
    }
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

// Hacer Program accesible para tests de integracion
public partial class Program { }
