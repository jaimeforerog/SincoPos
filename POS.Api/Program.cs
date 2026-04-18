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
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using POS.Infrastructure.Marten;
using POS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// HttpContextAccessor para auditoría
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<POS.Application.Services.ICurrentEmpresaProvider, POS.Infrastructure.Services.CurrentEmpresaProvider>();
builder.Services.AddScoped<POS.Application.Services.IEmpresaService, POS.Infrastructure.Services.EmpresaService>();

// Entity Framework Core (PostgreSQL - schema public)
builder.Services.AddAppDbContext(builder.Configuration);

// Services (ANTES de Marten - las projections usan BuildServiceProvider y necesitan estos servicios)
builder.Services.AddScoped<POS.Application.Services.ITerceroService, POS.Infrastructure.Services.TerceroLocalService>();
builder.Services.AddScoped<POS.Application.Services.IProductoService, POS.Infrastructure.Services.ProductoLocalService>();
builder.Services.AddScoped<POS.Application.Services.IProductoAnticipacionService, POS.Infrastructure.Services.ProductoAnticipacionService>();
builder.Services.AddScoped<POS.Application.Services.IAprendizajeService, POS.Infrastructure.Services.AprendizajeService>();
builder.Services.AddScoped<POS.Application.Services.IRadarNegocioService, POS.Infrastructure.Services.RadarNegocioService>();
builder.Services.AddScoped<POS.Application.Services.IPosContextoService, POS.Infrastructure.Services.PosContextoService>();
builder.Services.AddScoped<POS.Application.Services.ISugerenciasService, POS.Infrastructure.Services.SugerenciasService>();
builder.Services.AddScoped<POS.Application.Services.IClienteHistorialService, POS.Infrastructure.Services.ClienteHistorialService>();
builder.Services.AddScoped<POS.Infrastructure.Services.CosteoService>();
builder.Services.AddScoped<POS.Application.Services.IPrecioService, POS.Infrastructure.Services.PrecioService>();

// Identity Provider: WorkOS User Management API
builder.Services.Configure<POS.Infrastructure.Configuration.WorkOsOptions>(
    builder.Configuration.GetSection(POS.Infrastructure.Configuration.WorkOsOptions.SectionName));
builder.Services.AddHttpClient<POS.Application.Services.IIdentityProviderService, POS.Infrastructure.Services.WorkOsIdentityProviderService>();
builder.Services.AddHttpClient("workos");

// Usuario service: interface + concrete (concrete kept for backward compat with CajasController)
builder.Services.AddScoped<POS.Application.Services.IUsuarioService, POS.Infrastructure.Services.UsuarioService>();
builder.Services.AddScoped<POS.Infrastructure.Services.UsuarioService>();

builder.Services.AddScoped<POS.Infrastructure.Services.MigracionLogService>();
builder.Services.AddScoped<POS.Infrastructure.Services.ITaxEngine, POS.Infrastructure.Services.TaxEngine>();
builder.Services.AddScoped<POS.Application.Services.IVentaService, POS.Infrastructure.Services.VentaService>();
builder.Services.AddScoped<POS.Infrastructure.Services.VentaAnulacionService>();
builder.Services.AddScoped<POS.Infrastructure.Services.VentaDevolucionService>();
builder.Services.AddScoped<POS.Infrastructure.Services.CompraRecepcionService>();
builder.Services.AddScoped<POS.Infrastructure.Services.CompraDevolucionService>();
builder.Services.AddScoped<POS.Application.Services.ICompraService, POS.Infrastructure.Services.CompraService>();
builder.Services.AddScoped<POS.Infrastructure.Services.IVentaCosteoService, POS.Infrastructure.Services.VentaCosteoService>();
builder.Services.AddScoped<POS.Infrastructure.Services.ICompraErpService, POS.Infrastructure.Services.CompraErpService>();
builder.Services.AddScoped<POS.Infrastructure.Services.IVentaErpService, POS.Infrastructure.Services.VentaErpService>();
builder.Services.AddScoped<POS.Application.Services.ITrasladoService, POS.Infrastructure.Services.TrasladoService>();
builder.Services.AddScoped<POS.Application.Services.IInventarioService, POS.Infrastructure.Services.InventarioService>();
builder.Services.AddScoped<POS.Application.Services.ILoteService, POS.Infrastructure.Services.LoteService>();
builder.Services.AddScoped<POS.Application.Services.IReportesService, POS.Infrastructure.Services.ReportesService>();
builder.Services.AddScoped<POS.Application.Services.IEthicalGuardService, POS.Infrastructure.Services.EthicalGuardService>();
builder.Services.AddScoped<POS.Application.Services.IColectivaService, POS.Infrastructure.Services.ColectivaService>();
builder.Services.AddSingleton<POS.Application.Services.IPipelineMetricsService, POS.Infrastructure.Services.PipelineMetricsService>();
builder.Services.AddScoped<POS.Application.Services.ISaleOrchestrator, POS.Infrastructure.Services.SaleOrchestrator>();

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

// ── Observabilidad: OpenTelemetry (Tracing + Metrics) ────────────────────
var otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "sincopos-api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
                // Excluir health checks y SignalR negotiate para no saturar el backend de trazas
                opts.Filter = ctx =>
                    !ctx.Request.Path.StartsWithSegments("/health") &&
                    !(ctx.Request.Path.Value?.Contains("/negotiate") ?? false);
            })
            .AddHttpClientInstrumentation(opts => opts.RecordException = true);

        if (!string.IsNullOrEmpty(otlpEndpoint))
            tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(PipelineMetricsService.MeterName);  // métricas de negocio (ventas, pipeline)

        if (!string.IsNullOrEmpty(otlpEndpoint))
            metrics.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
    });

// Activity Log Service (Singleton para Channel-based background processing)
builder.Services.AddSingleton<POS.Application.Services.IActivityLogService, POS.Infrastructure.Services.ActivityLogService>();

// Facturación Electrónica DIAN
builder.Services.AddScoped<POS.Application.Services.IUblBuilderService, POS.Infrastructure.Services.UblBuilderService>();
builder.Services.AddScoped<POS.Application.Services.IFirmaDigitalService, POS.Infrastructure.Services.FirmaDigitalService>();
builder.Services.AddHttpClient<POS.Infrastructure.Services.DianSoapService>()
    .AddStandardResilienceHandler(options =>
    {
        // Circuit breaker: abre si ≥50% de fallos en 3+ intentos dentro de 1 min → pausa 30 min
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 3;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(30);
        // Reintentos: 2 intentos con backoff exponencial (1s, 2s)
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        // Timeout por intento: 30s × 3 intentos + 3s delays = 93s → total 100s
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
    });
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
builder.Services.AddHostedService<POS.Infrastructure.Services.AlertaVencimientoBackgroundService>();

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
        .SetVaryByHeader("X-Empresa-Id")   // clave de cache distinta por empresa
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
builder.Services.AddProblemDetails();
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

// Authentication & Authorization — WorkOS AuthKit
// Pre-cargar JWKS antes de registrar servicios para evitar .Result blocking en el delegate.
var workosClientId = builder.Configuration["WorkOs:ClientId"];
var jwksUri = $"https://api.workos.com/sso/jwks/{workosClientId}";
IList<SecurityKey> workosSigningKeys;
using (var jwksHttpClient = new System.Net.Http.HttpClient())
{
    var jwksJson = await jwksHttpClient.GetStringAsync(jwksUri);
    workosSigningKeys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwksJson).GetSigningKeys();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.Authority = "https://api.workos.com";
    options.Audience = workosClientId;
    options.RequireHttpsMetadata = true;

    // RefreshOnIssuerKeyNotFound = true: cuando llega un token firmado con una key que no
    // está en caché (rotación de WorkOS), el middleware descarga automáticamente el JWKS
    // actualizado y reintenta la validación, evitando el 401 por keys obsoletas.
    var signingKeys = workosSigningKeys;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuers = new[] {
            "https://api.workos.com",
            "https://api.workos.com/",
            $"https://api.workos.com/user_management/{workosClientId}"
        },
        ValidateAudience = true,
        ValidAudiences = new[] { workosClientId },
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKeys = signingKeys,
        ClockSkew = TimeSpan.FromMinutes(5),
        RoleClaimType = ClaimTypes.Role,
    };

    // Cuando un token usa una key no conocida (rotación), refrescar el JWKS automáticamente
    options.RefreshOnIssuerKeyNotFound = true;

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Soporte para SignalR: token en query string
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
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var rolesAdded = 0;
                var allClaims = context.Principal.Claims.ToList();

                // WorkOS: el rol viene en el claim "role" (string único)
                var workosRole = allClaims.FirstOrDefault(c => c.Type == "role")?.Value;
                if (!string.IsNullOrEmpty(workosRole))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, workosRole));
                    rolesAdded++;
                }

                // Fallback: buscar rol en BD usando el sub (WorkOS user ID)
                if (rolesAdded == 0)
                {
                    var externalId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Principal.FindFirstValue("sub");

                    if (!string.IsNullOrEmpty(externalId))
                    {
                        try
                        {
                            var dbContext = context.HttpContext.RequestServices
                                .GetRequiredService<POS.Infrastructure.Data.AppDbContext>();
                            var usuario = await dbContext.Set<POS.Infrastructure.Data.Entities.Usuario>()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.ExternalId == externalId);

                            if (usuario != null && !string.IsNullOrEmpty(usuario.Rol))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, usuario.Rol));
                                rolesAdded++;
                                logger.LogInformation("[WorkOS] Rol '{Role}' cargado desde BD para {Email}",
                                    usuario.Rol, usuario.Email);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "[WorkOS] Error al cargar rol desde BD para {Id}", externalId);
                        }
                    }
                }

                if (rolesAdded == 0)
                    logger.LogWarning("[WorkOS] Sin roles en token ni BD. Claims: {Claims}",
                        string.Join(", ", allClaims.Select(c => c.Type).Distinct()));
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
            policy.WithOrigins(
                      "http://localhost:5173",
                      "http://localhost:5174",
                      "http://localhost:5175",
                      "http://localhost:5176",
                      "http://localhost:5177",
                      "http://localhost:4173"
                  )
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
            // Verificar si las tablas ya existen (historial de migraciones perdido)
            var tablasExisten = await db.Database
                .SqlQueryRaw<int>(@"SELECT COUNT(*)::int AS ""Value"" FROM information_schema.tables WHERE table_schema='public' AND table_name='categorias'")
                .FirstOrDefaultAsync() > 0;

            if (tablasExisten)
            {
                // Las tablas ya existen pero el historial está incompleto: registrar sin ejecutar DDL
                migrationLogger.LogWarning(
                    "Historial de migraciones incompleto ({Count} pendientes). Tablas ya existen: registrando sin ejecutar DDL.",
                    pending.Count);
                // Sanitize: migration IDs are generated internally by EF Core (alphanumeric + underscore).
                // We validate the format before embedding to prevent any future misuse.
                var safePending = pending
                    .Where(m => System.Text.RegularExpressions.Regex.IsMatch(m, @"^[\w]+$"))
                    .ToList();
                if (safePending.Count != pending.Count)
                    throw new InvalidOperationException("Migration ID contiene caracteres inválidos.");
                foreach (var m in safePending)
                    await db.Database.ExecuteSqlAsync(
                        $"""
                        INSERT INTO public.__ef_migrations_history ("MigrationId", "ProductVersion")
                        VALUES ({m}, '9.0.3')
                        ON CONFLICT ("MigrationId") DO NOTHING
                        """);
                migrationLogger.LogWarning("Historial sincronizado: {Count} registros añadidos.", pending.Count);
            }
            else
            {
                migrationLogger.LogWarning("Aplicando {Count} migraciones pendientes: {Migrations}",
                    pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                migrationLogger.LogWarning("Migraciones aplicadas exitosamente");
            }
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

                -- columnas de auditoría en Empresas (AddEmpresaAuditoria)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='Empresas' AND column_name='CreadoPor')
                THEN
                    ALTER TABLE public.""Empresas"" ADD COLUMN ""CreadoPor"" text NOT NULL DEFAULT '';
                    RAISE NOTICE 'Columna CreadoPor agregada a Empresas';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='Empresas' AND column_name='ModificadoPor')
                THEN
                    ALTER TABLE public.""Empresas"" ADD COLUMN ""ModificadoPor"" text;
                    RAISE NOTICE 'Columna ModificadoPor agregada a Empresas';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='Empresas' AND column_name='FechaModificacion')
                THEN
                    ALTER TABLE public.""Empresas"" ADD COLUMN ""FechaModificacion"" timestamp with time zone;
                    RAISE NOTICE 'Columna FechaModificacion agregada a Empresas';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='Empresas' AND column_name='FechaDesactivacion')
                THEN
                    ALTER TABLE public.""Empresas"" ADD COLUMN ""FechaDesactivacion"" timestamp with time zone;
                    RAISE NOTICE 'Columna FechaDesactivacion agregada a Empresas';
                END IF;

                -- EmpresaId en entidades transaccionales (AddEmpresaIdTransactional)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='ventas' AND column_name='EmpresaId')
                THEN ALTER TABLE public.ventas ADD COLUMN ""EmpresaId"" integer; END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='traslados' AND column_name='EmpresaId')
                THEN ALTER TABLE public.traslados ADD COLUMN ""EmpresaId"" integer; END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='ordenes_compra' AND column_name='EmpresaId')
                THEN ALTER TABLE public.ordenes_compra ADD COLUMN ""EmpresaId"" integer; END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='documentos_electronicos' AND column_name='EmpresaId')
                THEN ALTER TABLE public.documentos_electronicos ADD COLUMN ""EmpresaId"" integer; END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='devoluciones_venta' AND column_name='EmpresaId')
                THEN ALTER TABLE public.devoluciones_venta ADD COLUMN ""EmpresaId"" integer; END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='cajas' AND column_name='EmpresaId')
                THEN ALTER TABLE public.cajas ADD COLUMN ""EmpresaId"" integer; END IF;

                -- Tablas AddEthicalGuard
                IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name='ReglasEticas')
                THEN
                    CREATE TABLE public.""ReglasEticas"" (
                        ""Id"" serial PRIMARY KEY,
                        ""EmpresaId"" integer,
                        ""Nombre"" text NOT NULL,
                        ""Contexto"" integer NOT NULL,
                        ""Condicion"" integer NOT NULL,
                        ""ValorLimite"" numeric NOT NULL,
                        ""Accion"" integer NOT NULL,
                        ""Mensaje"" text,
                        ""Activo"" boolean NOT NULL,
                        ""FechaCreacion"" timestamp with time zone NOT NULL
                    );
                    CREATE TABLE public.""ActivacionesReglaEtica"" (
                        ""Id"" serial PRIMARY KEY,
                        ""ReglaEticaId"" integer NOT NULL REFERENCES public.""ReglasEticas""(""Id"") ON DELETE CASCADE,
                        ""VentaId"" integer,
                        ""SucursalId"" integer,
                        ""UsuarioId"" integer,
                        ""Detalle"" text,
                        ""AccionTomada"" integer NOT NULL,
                        ""FechaActivacion"" timestamp with time zone NOT NULL
                    );
                    CREATE INDEX ""IX_ActivacionesReglaEtica_ReglaEticaId"" ON public.""ActivacionesReglaEtica""(""ReglaEticaId"");
                    RAISE NOTICE 'Tablas ReglasEticas y ActivacionesReglaEtica creadas';
                END IF;
            END $$;
        ");
    }
    catch (Exception ex)
    {
        migrationLogger.LogError(ex, "Error al sincronizar schema de BD");
    }
}

// ── Seed: Empresa por defecto y asignación a sucursales ─────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        // Crear empresa por defecto si no existe ninguna
        var tieneEmpresa = await db.Empresas.IgnoreQueryFilters().AnyAsync();
        if (!tieneEmpresa)
        {
            var empresa = new POS.Infrastructure.Data.Entities.Empresa
            {
                Nombre = "Empresa Principal",
                Nit    = "900000001-0",
                RazonSocial = "Empresa Principal S.A.S",
            };
            db.Empresas.Add(empresa);
            await db.SaveChangesAsync();
            seedLogger.LogWarning("Empresa por defecto creada con Id={Id}", empresa.Id);

            // Asignar todas las sucursales sin empresa a esta empresa
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE public.sucursales SET "EmpresaId" = {empresa.Id} WHERE "EmpresaId" IS NULL""");
            seedLogger.LogWarning("Sucursales sin empresa asignadas a EmpresaId={Id}", empresa.Id);
        }
    }
    catch (Exception ex)
    {
        seedLogger.LogError(ex, "Error en seed de empresa por defecto");
    }
}

// ── Escalabilidad: Global Exception Handler (ProblemDetails RFC 7807) ────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";
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
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = "Error interno del servidor",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "Ha ocurrido un error interno. Contacte soporte.",
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = traceId }
        };
        await context.Response.WriteAsJsonAsync(problem);
    });
});

// ── Escalabilidad: Response Compression ──────────────────────────────────
app.UseResponseCompression();

// ── Seguridad: HTTP Security Headers ─────────────────────────────────────
app.Use(async (context, next) =>
{
    // Evita MIME-type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    // Evita clickjacking
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    // No exponer referrer fuera del origen
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // Deshabilitar APIs de hardware/ubicación no usadas
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=()");
    // HSTS: solo en producción — Azure LB ya termina TLS, no usar en dev
    if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        context.Response.Headers.Append("Strict-Transport-Security",
            "max-age=31536000; includeSubDomains");
    // CSP: para rutas API devuelve política restrictiva; Swagger UI necesita inline styles/scripts
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; frame-ancestors 'none'");
    }
    else
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'none'; frame-ancestors 'none'");
    }
    await next();
});

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

// ── Dev-only: diagnóstico de body en POST ────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api"))
        {
            context.Request.EnableBuffering();
            var diagLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            using var reader = new System.IO.StreamReader(
                context.Request.Body,
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            diagLogger.LogWarning(
                "DIAG POST {Path} | CT: {CT} | CL: {CL} | BodyLen: {Len} | Body: {Body}",
                context.Request.Path,
                context.Request.ContentType ?? "(none)",
                context.Request.ContentLength?.ToString() ?? "(null)",
                rawBody.Length,
                rawBody.Length > 500 ? rawBody[..500] : rawBody);
        }
        await next();
    });
}

// DESARROLLO: autenticación/autorización permisiva
// PRODUCCION: Azure AD B2C
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<POS.Api.Middleware.EmpresaContextMiddleware>();
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

// ── Dev-only: diagnóstico ─────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/diagnostico", async (POS.Infrastructure.Data.AppDbContext db) =>
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var empresasList = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT e.""Id"", e.""Nombre"", e.""Activo"",
                (SELECT COUNT(*) FROM public.sucursales s WHERE s.""EmpresaId"" = e.""Id"") AS sucursales,
                (SELECT COUNT(*) FROM public.productos p WHERE p.""EmpresaId"" = e.""Id"") AS productos
                FROM public.""Empresas"" e ORDER BY e.""Id""";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                empresasList.Add(new { id = reader.GetInt32(0), nombre = reader.GetString(1), activo = reader.GetBoolean(2), sucursales = reader.GetInt64(3), productos = reader.GetInt64(4) });
        }

        await conn.CloseAsync();
        return Results.Ok(new { empresas = empresasList });
    }).AllowAnonymous();
}

// ── Dev-only: endpoint temporal para seed de datos (ya ejecutado, deshabilitado) ──
if (false && app.Environment.IsDevelopment())
{
    app.MapPost("/dev/seed-supermercado", async (POS.Infrastructure.Data.AppDbContext db) =>
    {
        // Paso 1: insertar categorías
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO public.categorias
              (nombre, descripcion, activo, ""CreadoPor"", ""FechaCreacion"",
               categoria_padre_id, nivel, ruta_completa, margen_ganancia, ""EmpresaId"")
            VALUES
              ('Lácteos',           'Leches, quesos, yogures y mantequillas',   true, 'seed', NOW(), NULL, 0, 'Lácteos',           0.30, 1),
              ('Carnes',            'Carnes rojas, aves y embutidos',           true, 'seed', NOW(), NULL, 0, 'Carnes',            0.25, 1),
              ('Frutas y Verduras', 'Frutas frescas y verduras de temporada',   true, 'seed', NOW(), NULL, 0, 'Frutas y Verduras', 0.35, 1),
              ('Panadería',         'Pan, galletas y pastelería',               true, 'seed', NOW(), NULL, 0, 'Panadería',         0.40, 1),
              ('Bebidas',           'Gaseosas, jugos, aguas y cervezas',        true, 'seed', NOW(), NULL, 0, 'Bebidas',           0.30, 1),
              ('Aseo del Hogar',    'Detergentes, desinfectantes y limpieza',   true, 'seed', NOW(), NULL, 0, 'Aseo del Hogar',    0.30, 1),
              ('Granos y Secos',    'Arroz, lentejas, fríjoles y pastas',       true, 'seed', NOW(), NULL, 0, 'Granos y Secos',    0.25, 1),
              ('Snacks',            'Papitas, chitos, chocolates y dulces',     true, 'seed', NOW(), NULL, 0, 'Snacks',            0.45, 1)
            ON CONFLICT DO NOTHING");

        // Paso 2: insertar 50 productos usando IDs de categorías recién creadas
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$
            DECLARE
              c_lacteos INT; c_carnes INT; c_frutas INT; c_panaderia INT;
              c_bebidas INT; c_aseo   INT; c_granos INT; c_snacks    INT;
            BEGIN
              SELECT ""Id"" INTO c_lacteos   FROM public.categorias WHERE nombre='Lácteos'            AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_carnes    FROM public.categorias WHERE nombre='Carnes'             AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_frutas    FROM public.categorias WHERE nombre='Frutas y Verduras'  AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_panaderia FROM public.categorias WHERE nombre='Panadería'          AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_bebidas   FROM public.categorias WHERE nombre='Bebidas'            AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_aseo      FROM public.categorias WHERE nombre='Aseo del Hogar'     AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_granos    FROM public.categorias WHERE nombre='Granos y Secos'     AND ""EmpresaId""=1 LIMIT 1;
              SELECT ""Id"" INTO c_snacks    FROM public.categorias WHERE nombre='Snacks'             AND ""EmpresaId""=1 LIMIT 1;

              INSERT INTO public.productos
                (id, codigo_barras, nombre, descripcion, categoria_id, precio_venta, precio_costo,
                 activo, fecha_creacion, ""CreadoPor"", unidad_medida, ""EsAlimentoUltraprocesado"",
                 maneja_lotes, ""EmpresaId"")
              VALUES
                (gen_random_uuid(),'7701234000001','Leche Entera 1L',          'Leche entera pasteurizada 1L',        c_lacteos,   3200,  2100,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000002','Leche Descremada 1L',      'Leche descremada 1 litro',            c_lacteos,   3400,  2200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000003','Queso Campesino 250g',     'Queso campesino fresco 250g',         c_lacteos,   5800,  3900,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000004','Yogur Natural 200g',       'Yogur natural sin azúcar 200g',       c_lacteos,   2900,  1800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000005','Mantequilla con Sal 100g', 'Mantequilla con sal 100g',            c_lacteos,   4200,  2800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000006','Crema de Leche 250ml',     'Crema de leche para cocinar 250ml',   c_lacteos,   4500,  2900,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000007','Queso Doble Crema 500g',   'Queso doble crema 500g',              c_lacteos,  11500,  7800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000011','Pechuga de Pollo 1kg',     'Pechuga de pollo sin hueso 1kg',      c_carnes,   14900,  9800,true,NOW(),'seed','KGM',false,false,1),
                (gen_random_uuid(),'7701234000012','Carne Molida 500g',        'Carne molida de res 500g',            c_carnes,   12500,  8200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000013','Chuleta de Cerdo 600g',    'Chuleta de cerdo 600g',               c_carnes,   13800,  9100,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000014','Salchicha Frankfurt 500g', 'Salchichas Frankfurt 500g',           c_carnes,    8900,  5800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000015','Jamón de Pierna 200g',     'Jamón de pierna rebanado 200g',       c_carnes,    7200,  4700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000016','Tocino Ahumado 200g',      'Tocino ahumado en lonjas 200g',       c_carnes,    8500,  5500,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000017','Muslo de Pollo 1kg',       'Muslo de pollo con hueso 1kg',        c_carnes,   10500,  6900,true,NOW(),'seed','KGM',false,false,1),
                (gen_random_uuid(),'7701234000021','Tomate Chonto 1kg',        'Tomate chonto fresco 1kg',            c_frutas,    3500,  2100,true,NOW(),'seed','KGM',false,false,1),
                (gen_random_uuid(),'7701234000022','Cebolla Cabezona 500g',    'Cebolla cabezona blanca 500g',        c_frutas,    2200,  1300,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000023','Zanahoria 1kg',            'Zanahoria fresca 1kg',                c_frutas,    2800,  1700,true,NOW(),'seed','KGM',false,false,1),
                (gen_random_uuid(),'7701234000024','Banano x6',                'Racimo de banano maduros x6',         c_frutas,    3200,  1900,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000025','Papa Pastusa 2kg',         'Papa pastusa lavada 2kg',             c_frutas,    5500,  3400,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000026','Aguacate Hass x2',         'Aguacate Hass maduros x2 unidades',   c_frutas,    4800,  3000,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000031','Pan Tajado Blanco 500g',   'Pan tajado blanco 500g',              c_panaderia, 4200,  2700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000032','Pan Integral 450g',        'Pan integral de trigo 450g',          c_panaderia, 5100,  3300,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000033','Galletas Saltinas x10',    'Galletas de soda Saltinas x10',       c_panaderia, 2800,  1700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000034','Croissant x4',             'Croissants de mantequilla x4',        c_panaderia, 6500,  4200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000035','Mogolla Integral x6',      'Mogollas integrales x6',              c_panaderia, 4900,  3100,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000041','Agua Cristal 600ml',       'Agua mineral natural 600ml',          c_bebidas,   1800,   900,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000042','Coca-Cola 1.5L',           'Gaseosa Coca-Cola 1.5 litros',        c_bebidas,   5500,  3200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000043','Jugo Hit Naranja 1L',      'Jugo de naranja Hit 1 litro',         c_bebidas,   4800,  2900,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000044','Café Colcafé 250g',        'Café molido Colcafé 250g',            c_bebidas,  16900, 10800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000045','Postobón Manzana 400ml',   'Gaseosa Postobón manzana 400ml',      c_bebidas,   2200,  1200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000046','Cerveza Águila 330ml',     'Cerveza Águila lata 330ml',           c_bebidas,   3500,  2200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000047','Té Hatsu Mora 475ml',      'Bebida de té con mora 475ml',         c_bebidas,   4200,  2600,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000051','Detergente Ariel 1kg',     'Detergente en polvo Ariel 1kg',       c_aseo,     18500, 12100,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000052','Jabón Rey x3',             'Jabón de lavar Rey x3 barras',        c_aseo,      5900,  3700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000053','Suavitel Lavanda 1L',      'Suavizante de ropa Suavitel 1L',      c_aseo,      9800,  6200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000054','Ajax Limpiador 500ml',     'Limpiador multiusos Ajax 500ml',      c_aseo,      7200,  4500,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000055','Esponja Scotch-Brite x2',  'Esponjas de cocina Scotch-Brite x2',  c_aseo,      5500,  3400,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000056','Papel Higiénico Familia x4','Papel higiénico Familia x4',         c_aseo,      9900,  6400,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000061','Arroz Diana 2kg',          'Arroz blanco Diana 2kg',              c_granos,    8900,  5700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000062','Fríjol Bola Roja 500g',    'Fríjol bola roja seco 500g',          c_granos,    4200,  2700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000063','Lenteja Verde 500g',       'Lenteja verde seca 500g',             c_granos,    3800,  2400,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000064','Pasta Espagueti 400g',     'Pasta espagueti de trigo 400g',       c_granos,    3500,  2200,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000065','Maíz Pira 200g',           'Maíz pira para crispetas 200g',       c_granos,    2900,  1800,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000066','Azúcar Blanca 2kg',        'Azúcar blanca refinada 2kg',          c_granos,    7200,  4700,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000071','Papitas Margarita 70g',    'Papitas fritas Margarita sal 70g',    c_snacks,    2900,  1700,true,NOW(),'seed','94',true, false,1),
                (gen_random_uuid(),'7701234000072','Chitos 45g',               'Chitos de maíz 45g',                  c_snacks,    2200,  1300,true,NOW(),'seed','94',true, false,1),
                (gen_random_uuid(),'7701234000073','Chocolatina Jet 16g',      'Chocolatina Jet leche 16g',           c_snacks,    1800,  1000,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000074','Maní Salado Tosh 100g',    'Maní salado Tosh 100g',               c_snacks,    3500,  2100,true,NOW(),'seed','94',false,false,1),
                (gen_random_uuid(),'7701234000075','Gomitas Trolli 100g',      'Gomitas surtidas Trolli 100g',         c_snacks,    4200,  2600,true,NOW(),'seed','94',true, false,1),
                (gen_random_uuid(),'7701234000076','Bienestarina 500g',        'Bienestarina enriquecida 500g',       c_snacks,    5800,  3800,true,NOW(),'seed','94',false,false,1);
            END $$");

        var total = await db.Database.SqlQueryRaw<int>(@"SELECT COUNT(*)::int AS ""Value"" FROM public.productos WHERE ""CreadoPor""='seed'").FirstOrDefaultAsync();
        return Results.Ok(new { mensaje = "Seed ejecutado", productos = total });
    }).AllowAnonymous();
}

app.Run();

// Hacer Program accesible para tests de integracion
public partial class Program { }
