# HÁGALE · base segura de backend

Esta entrega es la primera base funcional del backend de HÁGALE para Colombia. No pretende afirmar que la aplicación móvil, los viajes ni los pagos estén terminados: deja el núcleo listo para construirlos de manera ordenada.

## Lo que ya funciona

- Registro e inicio de sesión con JWT, contraseñas robustas y bloqueo temporal después de intentos fallidos.
- Botón de ingreso con Google visible como muestra en las pantallas de ingresar y registrarse. La validación segura del token ya está preparada; el botón se vuelve funcional cuando se configure el `Authentication:Google:ClientId` real de Google Cloud.
- Pantallas iniciales de bienvenida, ingresar y registrarse en marco vertical 9:16, sin desplazamiento general de la pantalla; el formulario conserva sus paneles independientes.
- Marca HÁGALE conectada con los PNG originales entregados: `wwwroot/assets/hagale-logo-original-colorway-a.png` y `wwwroot/assets/hagale-logo-original-colorway-b.png`; la aplicación usa derivados transparentes `hagale-logo-yellow.png` para superficies negras y `hagale-logo-black.png` para superficies amarillas, sin redibujar la forma.
- Motociclista original conectado en `wwwroot/assets/hagale-moto-original.png`.
- Icono instalable de la PWA generado con el logo original centrado: `wwwroot/assets/hagale-app-icon-512.png`.
- El acceso y registro usan el tema de marca: superficie negra, lectura blanca y acciones/bordes amarillos, con campos oscuros de alto contraste.
- En teléfonos y computador conserva la pantalla inicial en formato vertical 9:16, como una app móvil; después de ingresar, cada módulo se abre en paneles independientes. La PWA permite orientación libre para que Windows no quede bloqueado en vertical.
- Perfil del usuario autenticado.
- Solicitud para ser conductor, registro de vehículo y carga privada de documentos obligatorios: identidad, licencia, tarjeta de propiedad, SOAT y tecnomecánica cuando aplique por antigüedad de la moto.
- Revisión administrativa de documentos y solicitud; la aprobación asigna el rol `Driver` solo cuando la moto está activa y todos los documentos obligatorios están aprobados.
- Bandeja administrativa paginada y filtrable por estado para revisar solicitudes de conductor.
- Interfaz web responsive en español para registro, ingreso, perfil, solicitud de conductor y revisión administrativa.
- Solicitudes de servicio con origen, destino, tipo de moto, método de pago preferido y modo de tarifa. El cliente puede compartir la ubicación actual o marcar de forma explícita los puntos A y B en el mapa antes de enviar la solicitud.
- Un cliente mantiene una sola solicitud abierta a la vez; el formulario de un nuevo recorrido se habilita al cancelar o finalizar la anterior, para evitar duplicados y confusión en el panel.
- Asignación inicial: un conductor aprobado, disponible y compatible por ciudad/vehículo puede aceptar una solicitud una sola vez; la aceptación lo deja ocupado y está protegida contra dobles tomas.
- Negociación inicial de precio: un conductor compatible puede enviar una contraoferta superior o igual a la tarifa mínima; mientras el pasajero decide queda reservado. El pasajero puede aceptarla o rechazarla y liberar la solicitud nuevamente.
- Seguimiento inicial del viaje: el conductor asignado puede marcarse en camino, confirmar que llegó a la recogida, iniciar el servicio y finalizarlo; al terminar vuelve a quedar disponible.
- Seguimiento compartido del servicio activo: después de la aceptación, el cliente puede ver el nombre del conductor, moto, placa, A, B y la última ubicación que el conductor eligió compartir con GPS. No se expone teléfono ni ubicación antes de asignar el servicio; el historial del cliente solo muestra los datos operativos de sus propias solicitudes.
- Actualización privada en tiempo real: cambios de solicitud, despacho, estado del viaje, ubicación compartida y revisión de habilitación llegan a las cuentas involucradas sin exponer datos del recorrido dentro del aviso. Si la conexión falla, la interfaz conserva consultas periódicas como respaldo.
- Panel operativo de conductor separado del panel de cliente: disponibilidad, radio de despacho, ubicación de trabajo, solicitudes cercanas, contraofertas y servicio activo.
- Avisos del conductor para nuevas solicitudes: sonido y voz en el navegador, además de notificación del sistema cuando el dispositivo y el contexto seguro las permiten. El conductor puede activarlos o desactivarlos desde su panel.
- Modo visual día/noche para el conductor: el modo día aumenta contraste, tamaño y peso de textos para sol directo; el modo noche reduce brillo y conserva amarillo/negro/blanco.
- Historial de recorridos del cliente separado de la solicitud nueva, con estados, acciones pendientes y acceso al seguimiento de los servicios activos.
- Instalación como aplicación web progresiva (PWA) desde el navegador, con manifest, iconos y caché de la interfaz para volver a abrir HÁGALE como aplicación.
- Centro de administración con apertura autenticada de los archivos privados cargados por el conductor; los documentos no se exponen como enlaces públicos.
- Despacho inicial por cercanía: las solicitudes compatibles se filtran por ciudad, vehículo y radio; si conductor y pasajero comparten ubicación, se ordenan por distancia directa a la recogida. La aceptación siempre es manual.
- Detalle de oferta para conductor: muestra distancia hasta A, trayecto A–B, total directo y una referencia de tarifa calculada con la regla activa y la distancia directa A–B cuando ambos puntos fueron compartidos. La referencia declara explícitamente lo que no incluye: calles, tráfico, tiempo, comisión ni pagos.
- Reglas de tarifa por ciudad y servicio, editables por administración; la tarifa mínima se valida al crear una solicitud y queda registrada en ella.
- Disponibilidad del conductor con reglas de negocio: solo un conductor aprobado con vehículo puede ponerse disponible.
- Contactos de confianza privados: cada cuenta puede registrarlos, actualizarlos o desactivarlos; otras cuentas no pueden acceder a ellos.
- Canales de emergencia configurables por ciudad y tipo, exclusivos para administración. No hay números rígidos en el código.
- Roles `Customer`, `Driver` y `Administrator`.
- Tras aprobar una solicitud de conductor, la cuenta actualiza de forma segura el token de la sesión vigente para incluir el nuevo rol, sin obligar al usuario a cerrar sesión ni volver a escribir su contraseña.
- Auditoría de acciones que cambian estado: actor cuando está autenticado, método, ruta, resultado y correlación; nunca guarda cuerpos, contraseñas, tokens ni documentos.
- SQL Server LocalDB para desarrollo, migraciones de Entity Framework y pruebas de dominio/aplicación.

## Lo que sigue pendiente

El panel conductor ya incorpora un mapa de despacho con OpenStreetMap, marcador de la moto y seguimiento GPS mientras el conductor está disponible o en servicio. Cuando existen A y B, la interfaz dibuja una ruta referencial por tramos y la actualiza cuando cambia la ubicación compartida; no debe interpretarse como navegación calle a calle. El conductor puede abrir voluntariamente la navegación externa hacia A o B; HÁGALE no inicia esa navegación ni envía ubicaciones a ese servicio hasta que el conductor toca el enlace. Las fases siguientes cubrirán navegación calle a calle dentro de HÁGALE, seguimiento del pasajero más completo, pagos reales, notificaciones push, accesibilidad y despliegue productivo. La asignación actual registra preferencia de pago y tarifa, pero no cobra ni transfiere dinero. Los contactos de confianza aún no comparten ubicación ni realizan llamadas: esas funciones se activarán solamente dentro de un viaje y con consentimiento explícito.

## Ruta para abrirla sin el computador prendido

La versión local depende del computador porque el PC está actuando como servidor. Para que HÁGALE abra aunque este equipo esté apagado, hay que publicarla en producción:

1. **Servidor web/API en la nube:** publicar `Hagale.Api` en un hosting para .NET, VPS o servicio administrado.
2. **Base de datos real:** cambiar SQL Server LocalDB por una base de datos administrada con copias de seguridad.
3. **Dominio y HTTPS:** usar un dominio propio, por ejemplo `https://hagale.app`, con certificado SSL. Esto también es necesario para GPS, Google y notificaciones confiables.
4. **Almacenamiento privado:** mover documentos de conductores a storage privado con permisos, no a carpetas locales.
5. **Secretos de producción:** JWT, Google, mapas, correo/SMS y pagos deben vivir en variables seguras, no en archivos del proyecto.
6. **Monitoreo y respaldos:** logs, alertas, copias de seguridad y plan de recuperación.

Mientras esté en local, el enlace del celular `http://192.168.1.53:5171/` solo funciona si el computador está encendido, conectado a la misma Wi‑Fi y con el servidor abierto.

La publicación elegida para producción usará el dominio con tilde `hágale.shop`. El dominio ya está comprado, pero todavía apunta al parking de Hostinger; no se debe cambiar el DNS hasta tener la IP pública del servidor. El orden y los valores están documentados en [DEPLOYMENT-HAGALE-SHOP.md](DEPLOYMENT-HAGALE-SHOP.md). La lista ejecutiva está en [PRODUCCION-HAGALE-CHECKLIST.md](PRODUCCION-HAGALE-CHECKLIST.md). También queda generado el paquete Release actualizado `artifacts/hagale-publish-final-20260913.zip`.

Si todavía no hay presupuesto para servidor, existe una ruta temporal para verla sin el computador encendido: [DEMO-GRATIS-RENDER.md](DEMO-GRATIS-RENDER.md). Esa demo usa `render.yaml` y `appsettings.Demo.json`; sirve para mostrar y probar, no para operar comercialmente.
Para facilitar la carga manual también queda `artifacts/hagale-source-render-20260913.zip`, un paquete de código fuente sin publicaciones compiladas ni documentos privados.

## Ruta para convertirla en app

Actualmente HÁGALE es una **PWA**: una aplicación web instalable desde el navegador. Es el camino correcto para validar producto rápido porque una sola base funciona en PC, Android y iPhone.

- **Android:** se puede instalar desde Chrome como PWA. Para Google Play, se empaqueta después como aplicación Android/TWA o con Capacitor.
- **iPhone:** se puede agregar desde Safari a la pantalla de inicio. Para App Store, se empaqueta después con una capa nativa y cuenta de Apple Developer.
- **Producción real:** antes de tiendas conviene tener dominio HTTPS, backend publicado, GPS/rutas, notificaciones push, términos/privacidad y pruebas en teléfonos reales.

## Ruta GPS y mapas

El GPS básico del teléfono lo entrega el navegador o la app nativa, pero requiere contexto seguro. En producción se necesita HTTPS. Para una experiencia tipo ride-hailing completa hacen falta servicios externos de mapas:

- **Geolocalización del dispositivo:** ubicación en vivo del conductor y del cliente cuando autorizan permiso.
- **Geocodificación:** convertir direcciones en coordenadas y coordenadas en direcciones.
- **Rutas y ETA:** calcular ruta por calles, distancia real, tiempo estimado y tráfico.
- **Navegación:** indicaciones giro a giro o apertura controlada de navegación externa.

La versión actual ya dibuja mapas y rutas referenciales; todavía no calcula navegación calle a calle ni tráfico real.

## Arquitectura

```text
API (HTTP, JWT, autorización, auditoría)
  └─ Application (casos de uso y contratos)
       └─ Domain (reglas de conductor, vehículo y documentos)
            └─ Infrastructure (Identity, SQL Server, archivos privados)
```

## Ejecutar localmente

Requisitos: .NET SDK 10 y SQL Server LocalDB (ambos están instalados en este equipo de desarrollo).

Para arrancar la versión local con una sola orden en Windows:

```powershell
.\scripts\run-local.ps1
```

El script muestra automáticamente el enlace del PC y el enlace para el celular en la misma red. Para verificar el proyecto sin iniciar la API:

```powershell
.\scripts\verify.ps1
```

Para probar el modo demo temporal, sin SQL Server LocalDB:

```powershell
.\scripts\run-demo.ps1
```

Ese modo usa una base en memoria y está pensado para validar la publicación
gratuita; al detener el proceso se borran sus datos.

Para crear una publicación Release lista para servidor:

```powershell
.\scripts\publish-release.ps1
```

El ZIP se crea dentro de `artifacts/` y el script muestra su SHA-256.

```powershell
dotnet tool restore
dotnet restore Hagale.sln

# Genera una clave local de 48 bytes y la guarda fuera del repositorio.
$jwtKey = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project src\Hagale.Api\Hagale.Api.csproj

# Opcional: activa el botón real de Google Identity Services.
dotnet user-secrets set "Authentication:Google:ClientId" "TU_CLIENT_ID_DE_GOOGLE.apps.googleusercontent.com" --project src\Hagale.Api\Hagale.Api.csproj

dotnet run --project src\Hagale.Api\Hagale.Api.csproj
```

En modo `Development`, la aplicación aplica las migraciones y crea únicamente los roles. Abre `http://localhost:5171/` para ver la interfaz web. El estado se consulta en `http://localhost:5171/health` y el documento OpenAPI de desarrollo en `http://localhost:5171/openapi/v1.json`.

### Activar Google

El código ya valida el `id_token` de Google en el servidor. Para activar el botón:

1. Entra a [Google Cloud Console](https://console.cloud.google.com/), crea o selecciona un proyecto y abre **Google Auth Platform > Clients**.
2. Crea un cliente **Web application**.
3. En **Authorized JavaScript origins** agrega `http://localhost:5171` para probar Google en el PC. Para Google desde el teléfono no uses la IP local HTTP: Google exige HTTPS en un dominio real; ese dominio se agrega cuando publiquemos la versión de pruebas o producción.
4. Copia el Client ID que termina en `.apps.googleusercontent.com` y guárdalo como secreto:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "TU_CLIENT_ID.apps.googleusercontent.com" --project src\Hagale.Api\Hagale.Api.csproj
```

5. Reinicia la API y verifica `http://localhost:5171/api/v1/auth/google/status`; `isConfigured` debe aparecer como `true`.

Para producción se reemplazan los orígenes locales por el dominio HTTPS real. Google requiere que el origen autorizado coincida con el dominio desde el que se abre la aplicación. La dirección `http://192.168.1.53:5171` sí sirve para probar HÁGALE en el teléfono, pero el botón de Google puede quedar bloqueado por la regla HTTPS de Google hasta que exista un dominio seguro.
El servidor envía `Cross-Origin-Opener-Policy: same-origin-allow-popups` para que el botón/popup de Google funcione de forma compatible con navegadores modernos.

### PC, Android e iPhone

HÁGALE es actualmente una aplicación web progresiva (PWA), no tres aplicaciones separadas:

- **PC:** se abre en Chrome, Edge o Firefox; Chrome/Edge permiten instalarla como aplicación desde el icono de instalación.
- **Android:** se abre en Chrome y puede instalarse con “Instalar aplicación”.
- **iPhone/iPad:** se abre en Safari y se agrega con “Compartir > Añadir a pantalla de inicio”.

La misma API, sesión y paneles se usan en los tres casos. Para publicar en Google Play o App Store habrá que empaquetar esta PWA con una capa nativa y configurar credenciales OAuth específicas de Android/iOS; eso es una fase posterior a la versión web responsive.

Para probarla desde un teléfono en la misma red Wi‑Fi del computador, inicia la API escuchando en la red local y abre `http://192.168.1.53:5171/` desde el teléfono. La dirección puede cambiar si el router asigna otra IP. Una cuenta con rol `Driver` puede alternar entre `Modo cliente` y `Modo conductor`; el servidor impide solicitar un servicio mientras el conductor esté disponible o atendiendo un viaje.

## Probar el despacho del conductor

1. Con una cuenta de cliente, crea una solicitud de `HÁGALE MOTO` en la misma ciudad registrada en la moto del conductor. Para probar distancia y el mapa A/B, escribe primero las direcciones y usa **“Usar mi ubicación actual”** o **“Elegir en el mapa”** en cada punto antes de solicitar.
2. Inicia sesión con el conductor aprobado, entra a **Modo conductor** y pulsa **Activar disponibilidad**.
3. Pulsa **Actualizar ubicación** y autoriza el navegador si estás en un entorno HTTPS. Elige el radio de despacho (3, 5, 8 o 15 km).
4. La solicitud aparece en **Solicitudes de viaje**. El conductor ve recogida, destino, oferta y, cuando hay ubicaciones, distancia al pasajero y los puntos A/B en el detalle del mapa.
5. El conductor puede **Aceptar servicio**, enviar una **Contraoferta** o elegir **No me interesa**. Una aceptación reserva el servicio y abre el tablero de viaje activo.

La ubicación compartida es opcional y se usa solo para ordenar el despacho mientras el conductor está disponible. Al desconectarse, se elimina de la cola de despacho. En un teléfono, los navegadores normalmente requieren HTTPS para dar acceso a GPS; la dirección HTTP local sirve para probar la interfaz, pero puede no permitir ubicación real.

La interfaz usa la misma API y no almacena el token de acceso de forma persistente: se elimina al cerrar la pestaña o cerrar sesión.

Para aplicar las migraciones manualmente:

```powershell
dotnet tool run dotnet-ef database update --project src\Hagale.Infrastructure\Hagale.Infrastructure.csproj --startup-project src\Hagale.Api\Hagale.Api.csproj
```

## Administrador de desarrollo

No existen credenciales administrativas predeterminadas. Para crear una cuenta local explícitamente, configura ambos secretos antes de iniciar la API:

```powershell
dotnet user-secrets set "DevelopmentBootstrap:AdministratorEmail" "admin@hagale.local" --project src\Hagale.Api\Hagale.Api.csproj
dotnet user-secrets set "DevelopmentBootstrap:AdministratorPassword" "UnaClaveLocal!123" --project src\Hagale.Api\Hagale.Api.csproj
```

Después de crearla y verificar el acceso, elimina esos dos secretos. En producción este mecanismo no debe utilizarse.

```powershell
dotnet user-secrets remove "DevelopmentBootstrap:AdministratorEmail" --project src\Hagale.Api\Hagale.Api.csproj
dotnet user-secrets remove "DevelopmentBootstrap:AdministratorPassword" --project src\Hagale.Api\Hagale.Api.csproj
```

## Verificaciones

```powershell
dotnet build Hagale.sln --no-restore
dotnet test Hagale.sln --no-restore
dotnet list Hagale.sln package --vulnerable
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src\Hagale.Infrastructure\Hagale.Infrastructure.csproj --startup-project src\Hagale.Api\Hagale.Api.csproj
```

## Seguridad local

La clave JWT y cualquier credencial van en User Secrets o variables de entorno, nunca en `appsettings*.json`. Los documentos cargados se guardan en `src/Hagale.Api/App_Data/`, que está excluida del repositorio. Para producción deberán reemplazarse por almacenamiento privado gestionado y secretos administrados.
