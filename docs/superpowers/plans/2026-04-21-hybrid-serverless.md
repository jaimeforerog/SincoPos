# Hybrid Serverless — ErpSync + Azure SignalR Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar `ErpSyncBackgroundService` a un Azure Function con Timer Trigger y reemplazar ASP.NET Core SignalR por Azure SignalR Service (Free Tier) como transporte.

**Architecture:** Se crea el proyecto `POS.Functions` (.NET 9 isolated worker) con `ErpSyncFunction` que reutiliza `POS.Infrastructure` directamente. La API agrega `.AddAzureSignalR()` — un cambio de una línea — y elimina el `BackgroundService`. El frontend no cambia.

**Tech Stack:** Azure Functions v4 isolated worker, `Microsoft.Azure.Functions.Worker.Extensions.SignalRService`, `Microsoft.Azure.SignalR`, EF Core 9 + Npgsql, Azurite (emulador local), Azure Functions Core Tools v4.

---

## File Map

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `POS.Functions/POS.Functions.csproj` | Crear | Proyecto Functions: referencias NuGet y a POS.Infrastructure |
| `POS.Functions/host.json` | Crear | Config del host: logging, retry |
| `POS.Functions/local.settings.json` | Crear | Secrets locales (NO commitear) |
| `POS.Functions/Program.cs` | Crear | DI: AppDbContext, IErpClient, ErpSincoOptions |
| `POS.Functions/Functions/ErpSyncFunction.cs` | Crear | Timer Trigger + SignalR output binding |
| `POS.Api/POS.Api.csproj` | Modificar | Agregar `Microsoft.Azure.SignalR` NuGet |
| `POS.Api/Program.cs` | Modificar | `AddAzureSignalR()`, eliminar `ErpSyncBackgroundService` |
| `POS.Api/appsettings.json` | Modificar | Agregar placeholder `AzureSignalRConnectionString` |
| `POS.Api/appsettings.Development.json` | Modificar | Agregar connection string real de dev |
| `POS.Infrastructure/Services/Erp/ErpSyncBackgroundService.cs` | Eliminar | Reemplazado por ErpSyncFunction |
| `SincoPos.sln` | Modificar | Registrar POS.Functions |

---

## Task 1: Provisionar Azure SignalR Service (Free Tier)

**Files:** Ninguno — solo Azure Portal/CLI

- [ ] **Step 1: Crear el recurso via Azure CLI**

```bash
# Si no tienes el resource group de SincoPos, créalo (omitir si ya existe)
az group create --name sincopos-rg --location eastus

# Crear SignalR Service Free tier
az signalr create \
  --name sincopos-signalr \
  --resource-group sincopos-rg \
  --sku Free_F1 \
  --service-mode Default

# Obtener la connection string
az signalr key list \
  --name sincopos-signalr \
  --resource-group sincopos-rg \
  --query "primaryConnectionString" \
  --output tsv
```

- [ ] **Step 2: Guardar la connection string**

Copiar el valor devuelto. Tiene el formato:
```
Endpoint=https://sincopos-signalr.service.signalr.net;AccessKey=XXXXX==;Version=1.0;
```

La necesitarás en los Tasks 2 y 3.

- [ ] **Step 3: Commit (no hay código que commitear aún)**

```bash
# Solo anotación — no hay archivos que commitear en este step
echo "Azure SignalR Service creado: sincopos-signalr (Free tier)"
```

---

## Task 2: Actualizar POS.Api para usar Azure SignalR Service

**Files:**
- Modify: `POS.Api/POS.Api.csproj`
- Modify: `POS.Api/Program.cs` (líneas 241 y 264)
- Modify: `POS.Api/appsettings.json`
- Modify: `POS.Api/appsettings.Development.json`

- [ ] **Step 1: Agregar NuGet `Microsoft.Azure.SignalR` a POS.Api.csproj**

Abrir `POS.Api/POS.Api.csproj` y agregar dentro del `<ItemGroup>` de PackageReferences:

```xml
<PackageReference Include="Microsoft.Azure.SignalR" Version="1.30.0" />
```

- [ ] **Step 2: Eliminar `ErpSyncBackgroundService` de Program.cs**

En `POS.Api/Program.cs` línea 241, eliminar la siguiente línea:

```csharp
builder.Services.AddHostedService<POS.Infrastructure.Services.Erp.ErpSyncBackgroundService>();
```

Dejar la línea 242 intacta:
```csharp
builder.Services.AddHostedService<POS.Infrastructure.Services.AlertaVencimientoBackgroundService>();
```

- [ ] **Step 3: Cambiar `AddSignalR()` por `AddSignalR().AddAzureSignalR()` en Program.cs**

En `POS.Api/Program.cs` línea 264, reemplazar:

```csharp
// SignalR
builder.Services.AddSignalR();
```

por:

```csharp
// SignalR — Azure SignalR Service como transporte (fallback a local si no hay connection string)
var azureSignalRCs = builder.Configuration["AzureSignalRConnectionString"];
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(azureSignalRCs))
    signalRBuilder.AddAzureSignalR(azureSignalRCs);
```

- [ ] **Step 4: Agregar placeholder en `appsettings.json`**

Agregar en `POS.Api/appsettings.json` (dentro del objeto raíz, antes de la llave de cierre):

```json
"AzureSignalRConnectionString": ""
```

- [ ] **Step 5: Agregar connection string real en `appsettings.Development.json`**

Agregar en `POS.Api/appsettings.Development.json`:

```json
"AzureSignalRConnectionString": "Endpoint=https://sincopos-signalr.service.signalr.net;AccessKey=XXXXX==;Version=1.0;"
```

Reemplazar `XXXXX==` con la connection string obtenida en Task 1.

- [ ] **Step 6: Verificar build**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet build POS.Api/POS.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
git add POS.Api/POS.Api.csproj POS.Api/Program.cs POS.Api/appsettings.json
git commit -m "feat(api): migrar a Azure SignalR Service y extraer ErpSync a Functions"
```

> ⚠️ NO agregar `appsettings.Development.json` al commit — contiene la connection string.

---

## Task 3: Crear proyecto `POS.Functions`

**Files:**
- Create: `POS.Functions/POS.Functions.csproj`
- Create: `POS.Functions/host.json`
- Create: `POS.Functions/local.settings.json`
- Create: `POS.Functions/Program.cs`

- [ ] **Step 1: Crear directorio**

```bash
mkdir -p C:/Users/jaime.forero/RiderProjects/SincoPos/POS.Functions/Functions
```

- [ ] **Step 2: Crear `POS.Functions.csproj`**

Crear `POS.Functions/POS.Functions.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>POS.Functions</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.2.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.3.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.SignalRService" Version="1.8.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\POS.Infrastructure\POS.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Crear `host.json`**

Crear `POS.Functions/host.json`:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    },
    "logLevel": {
      "default": "Information",
      "POS.Functions": "Information"
    }
  },
  "extensions": {
    "signalR": {
      "hubName": "notificaciones"
    }
  }
}
```

- [ ] **Step 4: Crear `local.settings.json`**

Crear `POS.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureSignalRConnectionString": "Endpoint=https://sincopos-signalr.service.signalr.net;AccessKey=XXXXX==;Version=1.0;",
    "ErpSinco__BaseUrl": "",
    "ErpSinco__MaxReintentos": "5",
    "ErpSinco__TimeoutSeconds": "30"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=sincopos;Username=postgres;Password=postgrade;"
  }
}
```

Reemplazar `XXXXX==` con la connection string real de Azure SignalR Service.

> ⚠️ Este archivo NO debe commitearse. Verificar que `.gitignore` lo excluya (ver Step siguiente).

- [ ] **Step 5: Verificar que `local.settings.json` está en `.gitignore`**

```bash
grep "local.settings.json" C:/Users/jaime.forero/RiderProjects/SincoPos/.gitignore
```

Si no aparece, agregar al `.gitignore` raíz:

```
POS.Functions/local.settings.json
```

- [ ] **Step 6: Crear `Program.cs`**

Crear `POS.Functions/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services.Erp;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // EF Core — usa el constructor simple de AppDbContext (sin ICurrentEmpresaProvider),
        // lo que hace que los filtros globales de multi-tenant pasen todo (comportamiento correcto
        // para servicios de background que procesan datos de todas las empresas).
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' no encontrada.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<ErpSincoOptions>(config.GetSection(ErpSincoOptions.SectionName));

        // ERP client: mock en dev (BaseUrl vacío), real en prod.
        // En local.settings.json, "ErpSinco__BaseUrl" mapea a config["ErpSinco:BaseUrl"].
        var erpBaseUrl = config["ErpSinco:BaseUrl"];
        if (string.IsNullOrEmpty(erpBaseUrl))
        {
            services.AddSingleton<IErpClient, MockErpClient>();
        }
        else
        {
            services.AddHttpClient<IErpClient, SincoErpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ErpSincoOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });
        }
    })
    .Build();

await host.RunAsync();
```

- [ ] **Step 7: Verificar que compila el proyecto Functions solo**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet build POS.Functions/POS.Functions.csproj
```

Expected: `Build succeeded. 0 Error(s)`

Si hay errores de versión de paquetes, ajustar las versiones en el .csproj a las disponibles con:
```bash
dotnet add POS.Functions/POS.Functions.csproj package Microsoft.Azure.Functions.Worker
dotnet add POS.Functions/POS.Functions.csproj package Microsoft.Azure.Functions.Worker.Extensions.Timer
dotnet add POS.Functions/POS.Functions.csproj package Microsoft.Azure.Functions.Worker.Extensions.SignalRService
```

- [ ] **Step 8: Commit**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
git add POS.Functions/POS.Functions.csproj POS.Functions/host.json POS.Functions/Program.cs
git commit -m "feat(functions): crear proyecto POS.Functions con DI base"
```

---

## Task 4: Implementar `ErpSyncFunction`

**Files:**
- Create: `POS.Functions/Functions/ErpSyncFunction.cs`

- [ ] **Step 1: Crear `ErpSyncFunction.cs`**

Crear `POS.Functions/Functions/ErpSyncFunction.cs`:

```csharp
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Functions.Functions;

public class ErpSyncFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpSyncFunction> _logger;
    private readonly ErpSincoOptions _options;

    public ErpSyncFunction(
        IServiceScopeFactory scopeFactory,
        ILogger<ErpSyncFunction> logger,
        IOptions<ErpSincoOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    [Function("ErpSync")]
    public async Task<ErpSyncOutput> Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("ErpSyncFunction iniciado en {Timestamp}", DateTime.UtcNow);

        var messages = new List<SignalRMessageAction>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var erpClient = scope.ServiceProvider.GetRequiredService<IErpClient>();

        var mensajes = await db.ErpOutboxMessages
            .Where(m => m.Estado == EstadoOutbox.Pendiente ||
                       (m.Estado == EstadoOutbox.Error && m.Intentos < _options.MaxReintentos))
            .OrderBy(m => m.FechaCreacion)
            .Take(10)
            .ToListAsync();

        if (mensajes.Count == 0)
        {
            _logger.LogDebug("Sin mensajes Outbox pendientes.");
            return new ErpSyncOutput { Messages = [] };
        }

        _logger.LogInformation("Procesando {Count} mensajes Outbox.", mensajes.Count);

        foreach (var mensaje in mensajes)
        {
            mensaje.Intentos++;

            if (mensaje.TipoDocumento is "VentaCompletada" or "AnulacionVenta")
                await ProcesarVentaAsync(db, erpClient, mensaje, messages);
            else if (mensaje.TipoDocumento is "CompraRecibida" or "NotaCreditoVenta")
                await ProcesarCompraAsync(db, erpClient, mensaje, messages);
            else
                MarcarComoError(mensaje, $"Tipo '{mensaje.TipoDocumento}' no soportado.");
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("ErpSyncFunction completado. {Count} notificaciones SignalR emitidas.",
            messages.Count);

        return new ErpSyncOutput { Messages = messages.ToArray() };
    }

    // ── Venta / Anulación ────────────────────────────────────────────────────

    private async Task ProcesarVentaAsync(
        AppDbContext db,
        IErpClient erpClient,
        ErpOutboxMessage mensaje,
        List<SignalRMessageAction> messages)
    {
        var payload = JsonSerializer.Deserialize<VentaErpPayload>(mensaje.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para VentaErpPayload.");
            return;
        }

        var esAnulacion = mensaje.TipoDocumento == "AnulacionVenta";
        var response = await erpClient.ContabilizarVentaAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = true;
                venta.FechaSincronizacionErp = DateTime.UtcNow;
                venta.ErpReferencia = response.ErpReferencia;
                venta.ErrorSincronizacion = null;
            }

            var tipoDoc = esAnulacion ? "AnulacionVenta" : "VentaCompletada";
            var numeroSoporte = esAnulacion ? $"ANU-{payload.NumeroVenta}" : payload.NumeroVenta;
            var docContable = await db.DocumentosContables
                .Where(d => d.TipoDocumento == tipoDoc && d.NumeroSoporte == numeroSoporte)
                .OrderByDescending(d => d.FechaCausacion)
                .FirstOrDefaultAsync();
            if (docContable != null)
            {
                docContable.SincronizadoErp = true;
                docContable.ErpReferencia = response.ErpReferencia;
                docContable.FechaSincronizacionErp = DateTime.UtcNow;
            }

            _logger.LogInformation("ERP_SYNC_OK Venta={NumeroVenta} Ref={Ref}",
                payload.NumeroVenta, response.ErpReferencia);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_sincronizado",
                esAnulacion ? "Anulación contabilizada" : "Venta contabilizada",
                $"{payload.NumeroVenta} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                "success"));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = false;
                venta.ErrorSincronizacion = response.MensajeError;
            }

            _logger.LogWarning("ERP_SYNC_ERROR Venta={NumeroVenta} Error={Error}",
                payload.NumeroVenta, response.MensajeError);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_error",
                "Error de Sincronización Contable",
                $"Fallo contabilizando {payload.NumeroVenta}: {response.MensajeError}",
                "error"));
        }
    }

    // ── Compra / Nota Crédito ────────────────────────────────────────────────

    private async Task ProcesarCompraAsync(
        AppDbContext db,
        IErpClient erpClient,
        ErpOutboxMessage mensaje,
        List<SignalRMessageAction> messages)
    {
        var payload = JsonSerializer.Deserialize<CompraErpPayload>(mensaje.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para CompraErpPayload.");
            return;
        }

        var esCompra = mensaje.TipoDocumento == "CompraRecibida";
        var tipoDoc = esCompra ? "RecepcionCompra" : "NotaCredito";
        var response = await erpClient.ContabilizarCompraAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            int sucursalIdNotif = payload.SucursalId;

            if (esCompra)
            {
                var orden = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
                if (orden != null)
                {
                    orden.SincronizadoErp = true;
                    orden.FechaSincronizacionErp = DateTime.UtcNow;
                    orden.ErpReferencia = response.ErpReferencia;
                    orden.ErrorSincronizacion = null;
                    sucursalIdNotif = orden.SucursalId;
                }
            }
            else
            {
                var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null)
                {
                    devolucion.SincronizadoErp = true;
                    devolucion.FechaSincronizacionErp = DateTime.UtcNow;
                    devolucion.ErpReferencia = response.ErpReferencia;
                    devolucion.ErrorSincronizacion = null;
                }
            }

            var docContable = await db.DocumentosContables
                .Where(d => d.TipoDocumento == tipoDoc && d.NumeroSoporte == payload.NumeroOrden)
                .OrderByDescending(d => d.FechaCausacion)
                .FirstOrDefaultAsync();
            if (docContable != null)
            {
                docContable.SincronizadoErp = true;
                docContable.ErpReferencia = response.ErpReferencia;
                docContable.FechaSincronizacionErp = DateTime.UtcNow;
            }

            _logger.LogInformation("ERP_SYNC_OK {Tipo}={Numero} Ref={Ref}",
                tipoDoc, payload.NumeroOrden, response.ErpReferencia);

            messages.Add(Notificacion(sucursalIdNotif,
                "erp_sincronizado",
                esCompra ? "Compra contabilizada" : "Nota crédito contabilizada",
                $"{payload.NumeroOrden} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                "success"));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            if (esCompra)
            {
                var orden = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
                if (orden != null) { orden.SincronizadoErp = false; orden.ErrorSincronizacion = response.MensajeError; }
            }
            else
            {
                var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null) { devolucion.SincronizadoErp = false; devolucion.ErrorSincronizacion = response.MensajeError; }
            }

            _logger.LogWarning("ERP_SYNC_ERROR {Tipo}={Numero} Error={Error}",
                tipoDoc, payload.NumeroOrden, response.MensajeError);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_error",
                "Error de Sincronización Contable",
                $"Fallo contabilizando {payload.NumeroOrden}: {response.MensajeError}",
                "error"));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SignalRMessageAction Notificacion(
        int sucursalId, string tipo, string titulo, string mensaje, string nivel)
    {
        var dto = new NotificacionDto(tipo, titulo, mensaje, nivel, DateTime.UtcNow);
        return new SignalRMessageAction("Notificacion")
        {
            GroupName = $"sucursal-{sucursalId}",
            Arguments = [dto]
        };
    }

    private void MarcarComoError(ErpOutboxMessage mensaje, string error)
    {
        mensaje.Estado = mensaje.Intentos >= _options.MaxReintentos
            ? EstadoOutbox.Descartado
            : EstadoOutbox.Error;
        mensaje.UltimoError = error;
        _logger.LogWarning("Outbox {Id} marcado como {Estado}: {Error}",
            mensaje.Id, mensaje.Estado, error);
    }
}

// El output binding declara DÓNDE van los mensajes que retorna la función.
// El runtime de Azure Functions los entrega automáticamente a Azure SignalR Service.
public class ErpSyncOutput
{
    [SignalROutput(HubName = "notificaciones", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public SignalRMessageAction[] Messages { get; set; } = [];
}
```

- [ ] **Step 2: Build del proyecto Functions**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet build POS.Functions/POS.Functions.csproj
```

Expected: `Build succeeded. 0 Error(s)`

Si hay error `CS0246` en `SignalRMessageAction`: el paquete `Microsoft.Azure.Functions.Worker.Extensions.SignalRService` no resolvió — ejecutar:
```bash
dotnet restore POS.Functions/POS.Functions.csproj
dotnet build POS.Functions/POS.Functions.csproj
```

- [ ] **Step 3: Commit**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
git add POS.Functions/Functions/ErpSyncFunction.cs
git commit -m "feat(functions): implementar ErpSyncFunction con Timer Trigger y SignalR output binding"
```

---

## Task 5: Registrar POS.Functions en la solución

**Files:**
- Modify: `SincoPos.sln`

- [ ] **Step 1: Agregar a la solución**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet sln SincoPos.sln add POS.Functions/POS.Functions.csproj
```

Expected output: `Project 'POS.Functions\POS.Functions.csproj' added to the solution.`

- [ ] **Step 2: Build de la solución completa**

```bash
dotnet build SincoPos.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
git add SincoPos.sln
git commit -m "chore: agregar POS.Functions a la solución"
```

---

## Task 6: Eliminar `ErpSyncBackgroundService.cs`

**Files:**
- Delete: `POS.Infrastructure/Services/Erp/ErpSyncBackgroundService.cs`

- [ ] **Step 1: Eliminar el archivo**

```bash
rm C:/Users/jaime.forero/RiderProjects/SincoPos/POS.Infrastructure/Services/Erp/ErpSyncBackgroundService.cs
```

- [ ] **Step 2: Build completo para verificar no hay referencias rotas**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet build SincoPos.sln
```

Expected: `Build succeeded. 0 Error(s)`

Si hay error `CS0246: POS.Infrastructure.Services.Erp.ErpSyncBackgroundService no encontrado`: verificar que el `AddHostedService<ErpSyncBackgroundService>()` fue eliminado de `Program.cs` en Task 2.

- [ ] **Step 3: Commit**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
git add -u POS.Infrastructure/Services/Erp/ErpSyncBackgroundService.cs
git commit -m "refactor: eliminar ErpSyncBackgroundService (reemplazado por POS.Functions)"
```

---

## Task 7: Verificar ejecución local

**Prerequisitos:** Azure Functions Core Tools v4, Azurite (emulador Storage)

- [ ] **Step 1: Instalar Azure Functions Core Tools v4 (si no está instalado)**

```bash
# Verificar si ya está instalado
func --version
# Debe mostrar 4.x.x

# Si no está instalado:
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

- [ ] **Step 2: Instalar Azurite (emulador de Azure Storage)**

```bash
# Verificar si ya está instalado
azurite --version

# Si no está instalado:
npm install -g azurite
```

- [ ] **Step 3: Arrancar Azurite en una terminal separada**

```bash
azurite --silent --location C:/azurite --debug C:/azurite/debug.log
```

Mantener esta terminal abierta durante las pruebas.

- [ ] **Step 4: Arrancar la Function**

En una nueva terminal:
```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos/POS.Functions
func start
```

Expected output:
```
Azure Functions Core Tools (4.x)
...
[yyyy-mm-dd] ErpSync: timerTrigger running
```

- [ ] **Step 5: Verificar que dispara cada 30 segundos**

Observar la terminal. Cada 30 segundos debe aparecer:
```
[yyyy-mm-dd] Executing 'Functions.ErpSync' ...
[yyyy-mm-dd] ErpSyncFunction iniciado en 2026-...
[yyyy-mm-dd] Sin mensajes Outbox pendientes.
[yyyy-mm-dd] Executed 'Functions.ErpSync' (Succeeded, ...)
```

Si hay mensajes en la tabla `erp_outbox_messages` con `Estado = 0 (Pendiente)`, los procesará e intentará contactar al ERP (mock en dev — siempre retorna éxito).

- [ ] **Step 6: Verificar que la API sigue funcionando con Azure SignalR Service**

```bash
cd C:/Users/jaime.forero/RiderProjects/SincoPos
dotnet run --project POS.Api/POS.Api.csproj --urls "http://localhost:5086"
```

Abrir el frontend (`npm run dev`) y verificar:
1. Login funciona (WorkOS)
2. La campanilla de notificaciones se conecta (no hay errores en consola del browser)
3. Los logs del API no muestran errores de SignalR

Si aparece `Failed to connect to Azure SignalR Service`: verificar que `AzureSignalRConnectionString` en `appsettings.Development.json` es correcta.

- [ ] **Step 7: Verificar entrega de notificación end-to-end (opcional)**

Insertar un mensaje de prueba en la BD:

```sql
INSERT INTO erp_outbox_messages (tipo_documento, entidad_id, payload, fecha_creacion, intentos, estado)
VALUES (
  'VentaCompletada',
  1,
  '{"NumeroVenta":"VTA-001","SucursalId":1,"Total":150000,"Items":[]}',
  NOW(),
  0,
  0
);
```

Observar en la terminal de Functions:
```
ERP_SYNC_OK Venta=VTA-001 Ref=...
ErpSyncFunction completado. 1 notificaciones SignalR emitidas.
```

Y en el frontend (si está conectado a la sucursal 1), debe aparecer una notificación en la campanilla.

---

## Resumen de commits esperados

```
chore: agregar POS.Functions a la solución
refactor: eliminar ErpSyncBackgroundService (reemplazado por POS.Functions)
feat(functions): implementar ErpSyncFunction con Timer Trigger y SignalR output binding
feat(functions): crear proyecto POS.Functions con DI base
feat(api): migrar a Azure SignalR Service y extraer ErpSync a Functions
docs: spec diseño híbrido serverless ErpSync + Azure SignalR Service    ← ya commiteado
```
