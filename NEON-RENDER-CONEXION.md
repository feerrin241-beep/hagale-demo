# Conectar la base permanente de HÁGALE

La aplicación ya incluye soporte para PostgreSQL. El servicio de Render
continúa usando `InMemory` hasta que se agregue la conexión privada de Neon;
así la demo no se cae mientras se configura la cuenta.

## 1. Copiar la conexión desde Neon

1. Entra al proyecto **HAGALE** en [Neon](https://console.neon.tech/).
2. Pulsa **Connect**.
3. Selecciona la base, el usuario y la opción **Pooled connection** si aparece.
4. Copia la cadena que empieza por `postgresql://`.

La cadena contiene una contraseña. No se debe publicar en GitHub, capturas ni
mensajes. Si se filtra, hay que cambiar la contraseña desde Neon.

## 2. Agregarla en Render

En Render abre el servicio `hagale-demo`, entra en **Environment** y agrega o
edita estas variables:

| Key | Value |
| --- | --- |
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__HagaleDatabase` | La cadena privada copiada de Neon |

Guarda con **Save changes** y deja que Render haga el nuevo deploy. HÁGALE
creará las tablas al iniciar la primera vez.

## 3. Comprobar

1. Abre `https://hagale-demo.onrender.com/health`.
2. Debe responder `Healthy`.
3. Regístrate con una cuenta de prueba.
4. Reinicia el servicio desde Render y comprueba que la cuenta siga existiendo.

La base de datos guarda usuarios, perfiles, vehículos, documentos registrados,
solicitudes, historial, reglas de tarifa, chat privado y calificaciones. Los
archivos de documentos todavía necesitan almacenamiento de objetos persistente;
el directorio gratuito de Render (`/tmp`) se borra al reiniciar.
