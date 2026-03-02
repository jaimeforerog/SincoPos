# SincoPos - Sistema de Punto de Venta

Sistema de Punto de Venta moderno con arquitectura limpia, Event Sourcing y autenticación con Keycloak.

## 🏗️ Arquitectura

- **Clean Architecture** con DDD (Domain-Driven Design)
- **Event Sourcing** con Marten para inventario
- **CQRS** para separación de comandos y consultas
- **PostgreSQL** para persistencia relacional y eventos
- **Keycloak** para autenticación y autorización
- **.NET 9.0**

## ✨ Características

### ✅ Implementado

- **Productos**: CRUD completo con categorías e impuestos
- **Inventario**: Event Sourcing con entradas, salidas, ajustes y devoluciones
- **Costeo**: 4 métodos (PEPS, UEPS, Promedio Ponderado, Última Compra)
- **Ventas**: Crear y anular ventas con cálculo de impuestos
- **Precios**: Precios por sucursal con fallback automático
- **Multi-Sucursal**: Configuración independiente por sucursal
- **Cajas**: Gestión de cajas registradoras
- **Autenticación**: OAuth2/OIDC con Keycloak
- **Roles**: Admin, Supervisor, Cajero, Vendedor
- **Tests**: 40+ pruebas de integración con Testcontainers

## 🚀 Inicio Rápido

### Prerrequisitos

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/)

### 1. Clonar el Repositorio

```bash
git clone https://github.com/tu-usuario/sincopos.git
cd sincopos
```

### 2. Levantar Infraestructura con Docker

```bash
# Levantar PostgreSQL y Keycloak
docker-compose up -d

# Esperar a que Keycloak esté listo (puede tomar 1-2 minutos)
docker logs -f sincopos-keycloak
# Presiona Ctrl+C cuando veas "Listening on: http://0.0.0.0:8080"
```

### 3. Configurar Keycloak

Opción A - **Automático** (recomendado):
```bash
bash scripts/keycloak-init.sh
```

Opción B - **Manual**:
Sigue la guía en [scripts/keycloak-setup.md](scripts/keycloak-setup.md)

### 4. Ejecutar Migraciones de Base de Datos

```bash
dotnet ef database update --project POS.Infrastructure --startup-project POS.Api
```

### 5. Ejecutar la API

```bash
dotnet run --project POS.Api
```

La API estará disponible en:
- **HTTP**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger

### 6. Probar Autenticación

#### Obtener Token

```bash
# Admin
curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=Admin123!" \
  -d "grant_type=password" | jq -r '.access_token'
```

#### Usar Token en Swagger

1. Abre http://localhost:5000/swagger
2. Click en el botón "Authorize" 🔓
3. En el campo "Value", pega: `Bearer TU_TOKEN_AQUI`
4. Click "Authorize" y luego "Close"

#### Llamar a la API con cURL

```bash
# Guardar token en variable
TOKEN=$(curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=Admin123!" \
  -d "grant_type=password" | jq -r '.access_token')

# Llamar a un endpoint protegido
curl -X GET http://localhost:5000/api/productos \
  -H "Authorization: Bearer $TOKEN"
```

## 👥 Usuarios de Prueba

| Email | Password | Rol | Permisos |
|-------|----------|-----|----------|
| admin@sincopos.com | Admin123! | admin | Acceso total |
| supervisor@sincopos.com | Supervisor123! | supervisor | Gestión operativa |
| cajero@sincopos.com | Cajero123! | cajero | Ventas y caja |
| vendedor@sincopos.com | Vendedor123! | vendedor | Solo ventas |

## 🧪 Ejecutar Pruebas

```bash
# Todas las pruebas
dotnet test

# Solo pruebas de integración
dotnet test tests/POS.IntegrationTests

# Con verbose
dotnet test --logger "console;verbosity=detailed"
```

## 📁 Estructura del Proyecto

```
SincoPos/
├── POS.Api/                    # Capa de presentación (API REST)
│   ├── Controllers/            # Endpoints de la API
│   ├── Extensions/             # Extensiones para Claims, etc.
│   └── Program.cs              # Configuración de la aplicación
├── POS.Application/            # Capa de aplicación
│   ├── DTOs/                   # Data Transfer Objects
│   ├── Services/               # Interfaces de servicios
│   └── Validators/             # Validaciones con FluentValidation
├── POS.Domain/                 # Capa de dominio (lógica de negocio)
│   ├── Aggregates/             # Aggregates de Event Sourcing
│   └── Events/                 # Eventos de dominio
├── POS.Infrastructure/         # Capa de infraestructura
│   ├── Data/                   # Entity Framework Core
│   │   ├── Entities/           # Entidades de base de datos
│   │   ├── Configurations/     # Configuraciones de EF Core
│   │   └── Migrations/         # Migraciones de EF Core
│   ├── Marten/                 # Configuración de Marten (Event Store)
│   └── Services/               # Implementaciones de servicios
├── tests/
│   └── POS.IntegrationTests/   # Pruebas de integración
├── scripts/                    # Scripts de utilidad
│   ├── keycloak-init.sh        # Inicialización automática de Keycloak
│   └── keycloak-setup.md       # Guía manual de configuración
└── docker-compose.yml          # Configuración de Docker
```

## 🔐 Autenticación y Autorización

### Arquitectura

```
┌─────────────┐      ┌──────────────┐      ┌─────────────┐
│   Cliente   │─────>│  Keycloak    │─────>│   POS API   │
│  (Frontend) │      │  (OAuth2/    │      │ (.NET JWT)  │
│             │<─────│   OIDC)      │      │             │
└─────────────┘      └──────────────┘      └─────────────┘
     │                       │                      │
     │ 1. Login              │ 2. Valida            │
     │                       │    credenciales      │
     │<──────────────────────│                      │
     │ 3. JWT Token          │                      │
     │                       │                      │
     │───────────────────────────────────────────>│
     │              4. API Request + Bearer Token  │
     │                                              │
     │<─────────────────────────────────────────────│
     │              5. Response                     │
```

### Políticas de Autorización

| Política | Roles Permitidos | Uso |
|----------|-----------------|-----|
| `Admin` | admin | Configuración del sistema |
| `Supervisor` | admin, supervisor | Gestión de sucursal |
| `Cajero` | admin, supervisor, cajero | Ventas y cajas |
| `Vendedor` | admin, supervisor, cajero, vendedor | Consultas básicas |

### Ejemplo de Uso en Controllers

```csharp
[Authorize]  // Requiere autenticación
[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "Supervisor")]  // Solo supervisores y admins
    public async Task<IActionResult> Crear(CrearProductoDto dto)
    {
        // Obtener usuario autenticado
        var keycloakId = User.GetKeycloakId();
        var email = User.GetEmail();
        var roles = User.GetRoles();

        // Tu lógica aquí
    }
}
```

## 🛠️ Tecnologías

- **Backend**: .NET 9.0, ASP.NET Core
- **Base de Datos**: PostgreSQL 16
- **Event Store**: Marten
- **Autenticación**: Keycloak
- **Validación**: FluentValidation
- **Testing**: xUnit, FluentAssertions, Testcontainers
- **Containerización**: Docker, Docker Compose
- **ORM**: Entity Framework Core 9

## 📊 Event Sourcing

El inventario usa Event Sourcing con los siguientes eventos:

- `EntradaCompraRegistrada`: Registro de entrada de mercancía
- `SalidaVentaRegistrada`: Salida por venta
- `DevolucionProveedorRegistrada`: Devolución a proveedor
- `AjusteInventarioRegistrado`: Ajustes manuales de inventario
- `StockMinimoActualizado`: Cambio de stock mínimo

### Proyecciones

- **Stock**: Cantidad actual y costo promedio
- **Movimientos**: Historial de todos los movimientos
- **Lotes**: Control de lotes para PEPS/UEPS

## 🔧 Configuración

### Variables de Entorno

Copia `.env.example` a `.env` y ajusta según necesites:

```bash
cp .env.example .env
```

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=sincopos;Username=posuser;Password=pospass123"
  },
  "Authentication": {
    "Authority": "http://localhost:8080/realms/sincopos",
    "Audience": "pos-api",
    "RequireHttpsMetadata": false
  }
}
```

## 📝 Roadmap

### Próximas Características

- [ ] Dashboard con métricas en tiempo real
- [ ] Reportes de ventas y rentabilidad
- [ ] Frontend web (React/Blazor)
- [ ] App móvil para inventario
- [ ] Facturación electrónica
- [ ] Descuentos y promociones
- [ ] Devoluciones de clientes
- [ ] Integración con pasarelas de pago
- [ ] Multi-tenancy

## 🤝 Contribuir

1. Fork el proyecto
2. Crea una rama para tu feature (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request

## 📄 Licencia

Este proyecto está bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para más detalles.

## 🙏 Agradecimientos

- [Keycloak](https://www.keycloak.org/) - Solución de autenticación
- [Marten](https://martendb.io/) - Event Store para .NET
- [FluentValidation](https://fluentvalidation.net/) - Validaciones fluidas
- [Testcontainers](https://testcontainers.com/) - Testing con containers

## 📞 Soporte

- 📧 Email: soporte@sincopos.com
- 💬 Discord: [SincoPOS Community](https://discord.gg/sincopos)
- 🐛 Issues: [GitHub Issues](https://github.com/tu-usuario/sincopos/issues)

---

**Hecho con ❤️ por el equipo de SincoPOS**
