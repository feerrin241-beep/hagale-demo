# HÁGALE · lista de salida a producción

Fecha de revisión: **12 de septiembre de 2026**

## Listo en el código

- [x] Registro e ingreso con JWT.
- [x] Roles de cliente, conductor y administrador.
- [x] Solicitud y revisión administrativa de conductor.
- [x] Solicitudes de viaje, despacho inicial, contraoferta y ciclo del viaje.
- [x] GPS del navegador y mapas de referencia.
- [x] Paneles separados para cliente, conductor y administración.
- [x] Modo día/noche del conductor con textos grandes y alto contraste.
- [x] Avisos de sonido, voz y notificación del navegador cuando el dispositivo lo permite.
- [x] PWA instalable en PC, Android y iPhone.
- [x] Logo y motociclista originales conectados a la interfaz.
- [x] `Dockerfile`, `docker-compose.production.example.yml` y `Caddyfile.example`.
- [x] Scripts de Windows para arrancar, verificar y publicar: `scripts/run-local.ps1`, `scripts/verify.ps1` y `scripts/publish-release.ps1`.
- [x] Ruta de demo pública gratis preparada con `render.yaml` y `src/Hagale.Api/appsettings.Demo.json`.
- [x] HSTS y cabeceras básicas de seguridad para producción.
- [x] Compilación y 49 pruebas automatizadas correctas.
- [x] Paquete Release actualizado: `artifacts/hagale-publish-final-20260913.zip`.

## Falta antes de abrirla al público

- [ ] Contratar un VPS o servidor compatible con ASP.NET Core. El creador web de Hostinger y Netlify no sustituyen la API ni SQL Server.
- [ ] Obtener la IP pública del servidor.
- [ ] Crear variables de producción: contraseña SQL, clave JWT de mínimo 32 bytes y, cuando se active, Client ID de Google.
- [ ] Levantar SQL Server, aplicar migraciones y levantar la API.
- [ ] Instalar Caddy/Nginx y activar HTTPS.
- [ ] Cambiar el DNS de `hágale.shop` hacia la IP pública del servidor.
- [ ] Probar `https://hágale.shop/health` antes de anunciarla.
- [ ] Configurar backups automáticos de base de datos y documentos privados.
- [ ] Configurar Google Login con el origen HTTPS real.
- [ ] Elegir el proveedor real de rutas/ETA, notificaciones push, correo/SMS y pagos.
- [ ] Publicar términos, privacidad, tratamiento de datos y proceso de soporte.

## No hacer todavía

- No publicar contraseñas, claves JWT, tokens ni archivos `.env` en el chat.
- No cambiar el DNS mientras el servidor no tenga una IP pública definitiva.
- No anunciar GPS, Google Login o pagos como definitivos desde la versión local.
- No usar la carpeta local de documentos como almacenamiento de producción sin copias y permisos.

## Orden de la siguiente sesión

1. Elegir el VPS y obtener su IP pública.
2. Copiar el proyecto o el paquete Release al servidor.
3. Configurar `.env.production` en el servidor.
4. Levantar base de datos, migraciones, API y proxy HTTPS.
5. Cambiar DNS y verificar el dominio.
6. Ejecutar la prueba completa con una cuenta cliente y una cuenta conductor aprobada.
