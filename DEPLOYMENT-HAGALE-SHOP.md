# Publicación de HÁGALE en `hágale.shop`

## Decisión confirmada

HÁGALE usará el dominio con tilde:

```text
hágale.shop
```

Los navegadores, DNS y algunos proveedores pueden mostrar internamente su equivalente IDN/Punycode. Es normal y no cambia la marca visible.

## Qué necesitamos contratar

No usaremos el creador de páginas de Hostinger ni un hosting web compartido para la API.

La opción económica compatible con el proyecto actual es:

- un VPS de Hostinger o un servicio cloud que soporte ASP.NET Core;
- una base de datos productiva protegida;
- HTTPS para el dominio;
- almacenamiento privado para los documentos de conductores;
- backups y variables de entorno seguras.

El proyecto ya incluye archivos base para ese camino:

- `Dockerfile`: empaqueta la API ASP.NET Core.
- `docker-compose.production.example.yml`: ejemplo para levantar HÁGALE y SQL Server en un VPS.
- `.env.production.example`: plantilla de secretos que se copia como `.env.production` en el servidor.
- `Caddyfile.example`: ejemplo de proxy HTTPS para `hágale.shop`.
- `src/Hagale.Api/appsettings.Production.json`: configuración segura de hosts permitidos.
- `artifacts/hagale-publish-final-20260913.zip`: publicación Release actualizada lista para copiar a un servidor .NET sin `appsettings.Development.json`.
- `scripts/publish-release.ps1`: genera una nueva publicación y calcula su SHA-256.

El servidor debe permitir:

- .NET 10 o una imagen Docker compatible;
- puertos HTTP/HTTPS;
- WebSockets para SignalR;
- firewall;
- copias de seguridad;
- variables de entorno.

La publicación Release actual fue verificada el **12 de septiembre de 2026**:

- compilación correcta;
- 49 pruebas automatizadas correctas;
- endpoint `/health` responde `Healthy`;
- paquete sin secretos ni documentos privados;
- SHA-256 del ZIP final: `2434922177869B1EEFDD7B010EDA7CD518635588913E38569BC70CE2474370CC`.

## Orden correcto de publicación

### 1. Crear el servidor

Cuando se contrate, se debe conservar:

- dirección IP pública;
- sistema operativo;
- usuario administrativo;
- método de acceso seguro.

Las contraseñas y claves no deben enviarse por el chat.

### 2. Preparar la aplicación

Antes de publicar se deben cambiar las configuraciones de desarrollo:

- SQL Server LocalDB por una base de datos productiva;
- almacenamiento local de documentos por almacenamiento privado;
- claves JWT de producción;
- credenciales de Google;
- claves del proveedor de mapas;
- configuración de correo y notificaciones;
- `ASPNETCORE_ENVIRONMENT=Production`.

En el servidor:

```bash
cp .env.production.example .env.production
```

Después se editan los valores reales dentro de `.env.production`. Ese archivo no debe subirse a repositorios ni enviarse por chat.

### 2.1. Levantar base de datos y migraciones

Con Docker instalado, el orden recomendado para la beta es:

```bash
docker compose -f docker-compose.production.example.yml --env-file .env.production up -d sqlserver
docker compose -f docker-compose.production.example.yml --env-file .env.production --profile tools run --rm hagale-migrations
docker compose -f docker-compose.production.example.yml --env-file .env.production up -d hagale-api
```

Si SQL Server aún está iniciando cuando corra la migración, se espera un minuto y se repite solo el comando de migraciones.

### 3. Configurar DNS en Hostinger

Cuando exista la IP pública del servidor, en **Dominios → DNS / Servidores de nombres** se creará el registro indicado por el proveedor. En una configuración típica:

```text
Tipo: A
Nombre: @
Valor: IP_PUBLICA_DEL_SERVIDOR
```

Para `www` se puede usar:

```text
Tipo: CNAME
Nombre: www
Valor: hágale.shop
```

El panel puede mostrar el dominio con su forma IDN codificada. Se debe conservar exactamente el valor que entregue Hostinger.

### 4. Activar HTTPS

No se debe publicar para usuarios antes de tener HTTPS. Es necesario para:

- GPS del navegador;
- instalación PWA;
- Google Login;
- notificaciones;
- protección de sesiones.

### 5. Probar antes de anunciarla

Se probará con dos cuentas separadas:

1. cliente;
2. conductor aprobado.

El recorrido de prueba será:

1. registro e ingreso;
2. carga y revisión de documentos;
3. aprobación administrativa;
4. modo conductor;
5. disponibilidad y GPS;
6. solicitud del cliente;
7. oferta o contraoferta;
8. aceptación;
9. conductor en camino;
10. llegada a recogida;
11. inicio del viaje;
12. finalización;
13. historial y cartera.

## Estado actual

El dominio ya está comprado y actualmente resuelve al parking DNS de Hostinger (`2.57.91.91`), no a HÁGALE. Esto es normal mientras no exista un servidor de producción. Todavía no se debe cambiar DNS ni comprar otro dominio; primero hay que elegir el VPS/servidor y obtener su IP pública. El siguiente dato técnico necesario será esa IP.
