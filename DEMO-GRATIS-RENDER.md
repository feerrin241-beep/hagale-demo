# HÁGALE · demo pública gratis sin dejar el PC encendido

Fecha: **13 de septiembre de 2026**

## Qué resuelve

Esta ruta permite abrir HÁGALE desde un celular con un enlace público HTTPS sin tener el computador prendido.

La plataforma queda disponible en una URL parecida a:

```text
https://hagale-demo.onrender.com
```

## Qué NO resuelve

Esta demo gratuita no es producción comercial:

- puede tardar cerca de un minuto en abrir si llevaba tiempo sin uso;
- los datos se pueden borrar cuando el servicio reinicia, se redespliega o se duerme;
- los documentos cargados son solo para prueba;
- no reemplaza SQL Server, backups, pagos reales, notificaciones push reales ni monitoreo comercial;
- no conviene anunciarla todavía como app final.

## Archivos preparados

- `render.yaml`: configuración de Render para crear un servicio web Docker gratis.
- `Dockerfile`: empaqueta HÁGALE.
- `src/Hagale.Api/appsettings.Demo.json`: activa modo demo con base de datos temporal en memoria.
- `scripts/run-demo.ps1`: permite probar ese mismo modo demo en el PC, sin LocalDB.

## Probar el modo demo en el PC antes de publicarlo

```powershell
.\scripts\run-demo.ps1
```

La dirección local será `http://localhost:5172/`. Los registros, solicitudes y
documentos de esta prueba desaparecen al detener la ventana; no uses esta base
temporal para datos reales.

## Orden para montarla gratis

1. Crear una cuenta en Render.
2. Subir este proyecto a un repositorio GitHub privado o público.
3. En Render, crear un **Blueprint** desde ese repositorio.
4. Render detectará `render.yaml` y creará el servicio `hagale-demo`.
5. Esperar el primer deploy.
6. Abrir la URL pública que Render entregue.
7. Probar en celular con esa URL HTTPS.

## Dominio `hágale.shop`

Por ahora no es obligatorio conectarlo. Podemos usar primero el enlace gratis `onrender.com`.

Cuando la demo funcione bien, se puede conectar `hágale.shop` al servicio de Render si el plan y la configuración vigente lo permiten. Si después queremos producción real, el dominio se cambia al VPS o servidor definitivo.

## Regla de seguridad

No enviar por chat:

- claves;
- tokens;
- contraseñas;
- archivos `.env`;
- credenciales de Google;
- accesos de Hostinger o Render.

Si se necesita configurar algo sensible, se hace directamente en el panel del proveedor.
