# Configuración de Keycloak para SincoPos

Este documento describe cómo configurar Keycloak para autenticación del sistema POS.

## 🚀 Inicio Rápido

### 1. Levantar Keycloak con Docker Compose

```bash
docker-compose up -d keycloak postgres-keycloak
```

Keycloak estará disponible en: http://localhost:8080

Credenciales de admin:
- **Usuario**: `admin`
- **Contraseña**: `admin`

### 2. Acceder a la Consola de Administración

1. Navega a http://localhost:8080
2. Click en "Administration Console"
3. Login con las credenciales de admin

## 🔧 Configuración Manual

### Paso 1: Crear Realm

1. En el menú desplegable superior izquierdo (dice "master"), click "Create Realm"
2. **Realm name**: `sincopos`
3. **Enabled**: ON
4. Click "Create"

### Paso 2: Crear Roles

En el menú lateral: **Realm roles** → **Create role**

Crear los siguientes roles:

| Nombre | Descripción |
|--------|-------------|
| `admin` | Administrador del sistema - Acceso total |
| `supervisor` | Supervisor de sucursal - Gestión operativa |
| `cajero` | Cajero - Ventas y caja |
| `vendedor` | Vendedor - Solo ventas |

### Paso 3: Crear Client para API

1. **Clients** → **Create client**

2. **General Settings**:
   - **Client type**: `OpenID Connect`
   - **Client ID**: `pos-api`
   - Click "Next"

3. **Capability config**:
   - **Client authentication**: `OFF` (para APIs públicas) o `ON` (para APIs privadas)
   - **Authorization**: `OFF`
   - **Standard flow**: `ON`
   - **Direct access grants**: `ON`
   - **Implicit flow**: `OFF`
   - **Service accounts roles**: `OFF` (o `ON` si necesitas M2M)
   - Click "Next"

4. **Login settings**:
   - **Valid redirect URIs**:
     - `http://localhost:5000/*`
     - `http://localhost:3000/*` (si tienes frontend)
     - `https://tudominio.com/*` (producción)
   - **Web origins**: `*` (desarrollo) o específico en producción
   - Click "Save"

### Paso 4: Configurar Client Scopes

1. En **Client scopes** → buscar `roles`
2. Click en `roles` → **Mappers** tab
3. Verificar que existe `realm roles` mapper

Si no existe, crear:
- **Add mapper** → **By configuration** → **User Realm Role**
- **Name**: `realm roles`
- **Token Claim Name**: `realm_access.roles`
- **Add to ID token**: ON
- **Add to access token**: ON
- **Add to userinfo**: ON

### Paso 5: Crear Usuarios de Prueba

#### Usuario Admin

1. **Users** → **Add user**
2. **Username**: `admin@sincopos.com`
3. **Email**: `admin@sincopos.com`
4. **Email verified**: ON
5. **First name**: `Admin`
6. **Last name**: `Sistema`
7. Click "Create"

8. Tab **Credentials**:
   - Click "Set password"
   - **Password**: `Admin123!`
   - **Temporary**: OFF
   - Click "Save"

9. Tab **Role mappings**:
   - Click "Assign role"
   - Buscar y seleccionar: `admin`
   - Click "Assign"

#### Usuario Cajero

1. **Users** → **Add user**
2. **Username**: `cajero@sincopos.com`
3. **Email**: `cajero@sincopos.com`
4. **Email verified**: ON
5. **First name**: `Juan`
6. **Last name**: `Cajero`
7. Click "Create"

8. Tab **Credentials**:
   - Password: `Cajero123!`
   - **Temporary**: OFF

9. Tab **Role mappings**:
   - Assign role: `cajero`

#### Usuario Vendedor

Repetir proceso con:
- **Email**: `vendedor@sincopos.com`
- **Password**: `Vendedor123!`
- **Role**: `vendedor`

## 🧪 Probar Autenticación

### Obtener Token con cURL

```bash
# Admin
curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=pos-api" \
  -d "username=admin@sincopos.com" \
  -d "password=Admin123!" \
  -d "grant_type=password"

# Cajero
curl -X POST http://localhost:8080/realms/sincopos/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=pos-api" \
  -d "username=cajero@sincopos.com" \
  -d "password=Cajero123!" \
  -d "grant_type=password"
```

### Usar Token en la API

```bash
# Guardar el access_token de la respuesta anterior
TOKEN="eyJhbGciOiJSUzI1NiIsInR5cCI..."

# Llamar a un endpoint protegido
curl -X GET http://localhost:5000/api/productos \
  -H "Authorization: Bearer $TOKEN"
```

## 📝 Configuración Avanzada

### Personalizar Token Lifetime

**Realm settings** → **Tokens** tab:
- **Access Token Lifespan**: `30 minutes` (default: 5 min)
- **Refresh Token Lifespan**: `30 days`

### Habilitar User Registration

**Realm settings** → **Login** tab:
- **User registration**: ON (si quieres que usuarios se registren)

### Configurar Password Policy

**Authentication** → **Policies** tab → **Password policy**:
- Minimum length: 8
- Require digits: 1
- Require lowercase: 1
- Require uppercase: 1
- Require special characters: 1

## 🔄 Script de Automatización (Opcional)

Para automatizar la configuración, puedes usar Keycloak Admin CLI:

```bash
# Conectar al contenedor
docker exec -it sincopos-keycloak bash

# Login como admin
/opt/keycloak/bin/kcadm.sh config credentials \
  --server http://localhost:8080 \
  --realm master \
  --user admin \
  --password admin

# Crear realm
/opt/keycloak/bin/kcadm.sh create realms \
  -s realm=sincopos \
  -s enabled=true

# Crear roles
/opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=admin
/opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=supervisor
/opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=cajero
/opt/keycloak/bin/kcadm.sh create roles -r sincopos -s name=vendedor

# Ver más comandos en la documentación de Keycloak CLI
```

## 🛠️ Troubleshooting

### No puedo acceder a Keycloak

1. Verificar que el contenedor está corriendo:
   ```bash
   docker ps | grep keycloak
   ```

2. Ver logs:
   ```bash
   docker logs sincopos-keycloak
   ```

### Tokens no validan en la API

1. Verificar que `Authority` en `appsettings.Development.json` coincide con el realm
2. Verificar que `Audience` coincide con el `client_id`
3. Revisar logs de la API para ver errores de validación

### Usuario no tiene rol en el token

1. Verificar que el rol está asignado al usuario
2. Verificar que el mapper de roles está configurado en el client scope
3. Obtener un nuevo token (los tokens no se actualizan automáticamente)

## 📚 Referencias

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [Keycloak Admin CLI](https://www.keycloak.org/docs/latest/server_admin/#admin-cli)
- [OIDC Standard](https://openid.net/connect/)
