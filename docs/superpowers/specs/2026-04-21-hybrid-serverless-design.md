# Diseño: Enfoque Híbrido Serverless — ErpSync + Azure SignalR Service

**Fecha:** 2026-04-21  
**Objetivo:** Aprendizaje práctico de Azure Functions — migrar `ErpSyncBackgroundService` a un Timer Trigger y reemplazar ASP.NET Core SignalR por Azure SignalR Service.  
**Alcance:** `ErpSyncBackgroundService` únicamente. Los otros dos BackgroundService (Facturación, Alertas) se quedan en la API.

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│  Cliente React (frontend)                               │
│  @microsoft/signalr JS client                           │
└──────┬─────────────────────────────┬────────────────────┘
       │ 1. POST /hub/negotiate       │ 3. Recibe mensajes
       ▼                              ▼
┌─────────────────┐          ┌──────────────────────┐
│  POS.Api        │          │  Azure SignalR Service│
│  (App Service)  │◄────────►│  Free Tier            │
│                 │          │  grupos: sucursal-{id}│
│  /hub/negotiate │          └──────────┬───────────┘
│  (auth JWT)     │                     │ 4. Entrega
└─────────────────┘                     │    mensajes
                             ┌──────────▼───────────┐
                             │  POS.Functions        │
                             │  Consumption Plan     │
                             │                       │
                             │  ErpSyncFunction      │
                             │  Timer: */30 * * * * *│
                             │  → lee ErpOutbox (EF) │
                             │  → llama IErpClient   │
                             │  → output binding     │
                             │    SignalR            │
                             └──────────────────────┘
```

---

## Componentes

### 1. Nuevo proyecto `POS.Functions`

**Runtime:** .NET 9, Azure Functions v4 isolated worker  
**NuGet:** `Microsoft.Azure.Functions.Worker`, `Microsoft.Azure.Functions.Worker.Extensions.Timer`, `Microsoft.Azure.Functions.Worker.Extensions.SignalRService`

```
POS.Functions/
├── POS.Functions.csproj
├── host.json
├── local.settings.json        ← no commitear
├── Program.cs                 ← DI: AppDbContext, IErpClient, IActivityLogService
└── Functions/
    └── ErpSyncFunction.cs     ← Timer Trigger + SignalR output binding
```

**`ErpSyncFunction.cs` — estructura:**

```csharp
public class ErpSyncFunction
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ErpSyncFunction(IServiceScopeFactory scopeFactory) { ... }

    [Function("ErpSync")]
    [SignalROutput(HubName = "notifications", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public async Task<SignalRGroupAction[]> Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer)
    {
        // lógica de ErpSyncBackgroundService
        // retorna SignalRGroupAction[] en lugar de llamar INotificationService
    }
}
```

**`Program.cs`:**

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<IErpClient, ErpSincoClient>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.Configure<ErpSincoOptions>(config.GetSection("ErpSinco"));
    })
    .Build();
```

**`local.settings.json`:**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureSignalRConnectionString": "Endpoint=https://...;AccessKey=...;Version=1.0;",
    "ConnectionStrings__DefaultConnection": "Host=localhost;Port=5432;Database=sincopos;Username=postgres;Password=postgrade;"
  }
}
```

---

### 2. Cambios en `POS.Api`

`AddAzureSignalR()` es un reemplazo transparente: el `NotificationHub`, el `IHubContext<NotificationHub>` y el `MapHub<>` existentes siguen funcionando sin modificaciones. Azure SignalR Service actúa como transporte — la API ni siquiera sabe que cambió.

#### 2a. Un cambio en `Program.cs`

```csharp
// Antes
builder.Services.AddSignalR();

// Después — único cambio en toda la API
builder.Services.AddSignalR().AddAzureSignalR(builder.Configuration["AzureSignalRConnectionString"]);
```

Agregar NuGet: `Microsoft.Azure.SignalR`

#### 2b. Eliminar de `Program.cs`

```csharp
services.AddHostedService<ErpSyncBackgroundService>();  // ← eliminado, vive en Functions
```

#### 2c. Eliminar archivo

- `POS.Infrastructure/Services/Erp/ErpSyncBackgroundService.cs`

#### 2d. Lo que NO cambia

- `NotificationHub.cs` — sin tocar
- `SignalRNotificationService` — sin tocar
- `INotificationService` — sin tocar
- Todos los controladores que llaman `INotificationService` — sin tocar
- Los otros dos BackgroundService — sin tocar

---

### 3. Cambios en el Frontend

**Ninguno.** El cliente JS de SignalR llama automáticamente a `/hubs/notifications/negotiate` — la misma URL de siempre. Azure SignalR Service intercepta esa llamada y redirige la conexión de forma transparente.

El resto — eventos `ReceiveNotification`, grupos, Zustand store — sin cambios.

---

## Recursos Azure

| Recurso | Tier | Costo |
|---|---|---|
| Azure Functions App | Consumption | ~$0 (primer millón ejecuciones/mes gratis) |
| Azure Storage Account | Standard LRS | ~$1–2/mes (requerido por Functions) |
| Azure SignalR Service | **Free** (20 conexiones, 20k msg/día) | $0 |

La misma `AzureSignalRConnectionString` se configura en:
- `POS.Functions` → `local.settings.json` (dev) / Function App Settings (prod)
- `POS.Api` → `appsettings.json` (dev) / App Service Configuration (prod)

---

## Flujo de datos completo

1. Cliente React llama `GET /hubs/notifications/negotiate` (igual que antes)
2. Azure SignalR Service intercepta, devuelve URL + token de conexión al cliente
3. Cliente conecta directamente a Azure SignalR Service y se une al grupo `sucursal-{id}` via `NotificationHub.OnConnectedAsync`
4. `ErpSyncFunction` dispara cada 30 segundos, procesa `ErpOutboxMessages` pendientes
5. Por cada mensaje procesado, retorna `SignalRGroupAction[]` con el grupo destino y el payload
6. Azure Functions runtime envía los mensajes a Azure SignalR Service via output binding
7. Azure SignalR Service entrega a todos los clientes conectados en ese grupo

---

## Conceptos aprendidos

| Concepto | Dónde aparece |
|---|---|
| **Timer Trigger** | `ErpSyncFunction` — reemplaza el loop `while(!stoppingToken)` |
| **Output Binding** | Retornar `SignalRGroupAction[]` en lugar de llamar a un servicio |
| **Isolated Worker Model** | `Program.cs` con `HostBuilder` — DI igual que ASP.NET Core |
| **Negotiate pattern** | Endpoint en API que media entre cliente y Azure SignalR Service |
| **Azure SignalR Service** | Servicio managed que reemplaza el hub ASP.NET Core |

---

## Fuera de alcance (esta iteración)

- `FacturacionBackgroundService` — requiere Azure Service Bus (siguiente iteración)
- `AlertaVencimientoBackgroundService` — Timer Trigger diario (trivial, puede hacerse después)
- Retry policy con Polly en la Function
- CI/CD para el proyecto Functions
