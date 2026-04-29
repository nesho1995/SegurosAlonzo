# Reclamos WhatsApp

Primera versión del sistema:

- Lee correos IMAP no leídos.
- Extrae datos de reclamos.
- Guarda en MySQL.
- Genera mensaje de WhatsApp.
- Muestra bandeja web para revisar.
- Incluye botón de envío por WhatsApp Cloud API, deshabilitado por configuración.

## 1. Crear BD

```bash
mysql -u root -p < Sql/01_create_database.sql
```

Cambiar contraseña en el SQL y en `appsettings.json`.

## 2. Restaurar paquetes

```bash
dotnet restore
```

## 3. Ejecutar

```bash
dotnet run
```

Abrir:

```txt
http://localhost:5000
```

## 4. Configuración importante

En `appsettings.json`:

- `Worker:Enabled`: activar procesamiento automático.
- `Email:Enabled`: activar lectura de correos.
- `Email:MarkAsRead`: poner true cuando ya estés seguro.
- `WhatsApp:Enabled`: activar solo cuando ya tengas Cloud API configurado.

## 5. Seguridad

No subir `appsettings.json` real con contraseñas a GitHub.
Usar variables de entorno en producción.
