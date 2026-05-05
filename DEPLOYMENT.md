# Guía de Despliegue — Sistema de Correduría Alonzo

## Requisitos del servidor

| Componente | Versión mínima |
|------------|----------------|
| Sistema operativo | Ubuntu 22.04 LTS (recomendado) |
| .NET Runtime | 8.0 (ASP.NET Core) |
| MariaDB | 10.6+ / MySQL 8.0+ |
| Nginx | cualquier versión reciente |
| Certbot | para certificado SSL gratuito (Let's Encrypt) |
| RAM | 1 GB mínimo, 2 GB recomendado |
| Disco | 10 GB mínimo |

---

## 1. Instalar .NET 8 Runtime en el servidor

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --runtime aspnetcore --channel 8.0
```

O desde los repositorios de Microsoft:
```bash
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0
```

Verificar:
```bash
dotnet --list-runtimes
# Debe aparecer: Microsoft.AspNetCore.App 8.0.x
```

---

## 2. Instalar MariaDB

```bash
sudo apt install mariadb-server -y
sudo mysql_secure_installation
```

Crear base de datos y usuario:
```sql
CREATE DATABASE reclamos_auto CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'seguros_user'@'localhost' IDENTIFIED BY 'TU_PASSWORD_SEGURA';
GRANT ALL PRIVILEGES ON reclamos_auto.* TO 'seguros_user'@'localhost';
FLUSH PRIVILEGES;
```

---

## 3. Publicar la aplicación

Desde la máquina de desarrollo:

```bash
# Compilar el frontend
cd ClientApp
npm install
# El vite.config.ts tiene root: 'ClientApp', correr desde la raíz del proyecto
cd ..
.\ClientApp\node_modules\.bin\vite build --config ClientApp/vite.config.ts   # Windows
# O en Linux/Mac:
./ClientApp/node_modules/.bin/vite build --config ClientApp/vite.config.ts

# Publicar el backend
dotnet publish -c Release -o ./publish
```

Subir la carpeta `publish/` al servidor (con scp, rsync, o un panel de hosting):
```bash
rsync -avz ./publish/ usuario@tu-servidor:/opt/seguros-alonzo/
```

---

## 4. Configurar `appsettings.json` en el servidor

Copia `appsettings.example.json` como `appsettings.json` en `/opt/seguros-alonzo/` y edita con los valores reales:

```bash
cp appsettings.example.json appsettings.json
nano appsettings.json
```

Valores a completar:

| Clave | Descripción |
|-------|-------------|
| `ConnectionStrings:Default` | Cadena de conexión con tu usuario/password de MariaDB |
| `Email:User` | Correo Gmail para leer reclamos entrantes |
| `Email:Password` | App password de Gmail (no la contraseña normal) |
| `Admin:WhatsAppNumber` | Número del administrador con código de país (ej: 50499887766) |
| `WhatsApp:PhoneNumberId` | ID del número en Meta Business Manager |
| `WhatsApp:AccessToken` | Token permanente de Meta (no el temporal de 24h) |
| `WhatsApp:WebhookVerifyToken` | Token secreto para verificar el webhook (cualquier texto seguro) |
| `WhatsApp:TemplateName` | Nombre de la plantilla aprobada en Meta (dejar vacío si no hay) |
| `AllowedHosts` | Tu dominio (ej: `segurosalonzo.com`) |

> **Seguridad:** `appsettings.json` está en `.gitignore` y NUNCA se sube al repositorio.

---

## 5. Configurar como servicio systemd

Crear el archivo de servicio:
```bash
sudo nano /etc/systemd/system/seguros-alonzo.service
```

Contenido:
```ini
[Unit]
Description=Sistema de Correduría Alonzo
After=network.target mariadb.service

[Service]
Type=notify
User=www-data
WorkingDirectory=/opt/seguros-alonzo
ExecStart=/usr/bin/dotnet /opt/seguros-alonzo/ReclamosWhatsApp.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
SyslogIdentifier=seguros-alonzo

[Install]
WantedBy=multi-user.target
```

Activar e iniciar:
```bash
sudo systemctl daemon-reload
sudo systemctl enable seguros-alonzo
sudo systemctl start seguros-alonzo
sudo systemctl status seguros-alonzo
```

Ver logs en tiempo real:
```bash
sudo journalctl -u seguros-alonzo -f
```

---

## 6. Configurar Nginx como reverse proxy con HTTPS

Instalar Nginx y Certbot:
```bash
sudo apt install nginx certbot python3-certbot-nginx -y
```

Crear configuración de Nginx:
```bash
sudo nano /etc/nginx/sites-available/seguros-alonzo
```

Contenido:
```nginx
server {
    server_name tu-dominio.com www.tu-dominio.com;

    # Límite de tamaño de subida (documentos/logos)
    client_max_body_size 10M;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }
}
```

Activar el sitio y obtener SSL:
```bash
sudo ln -s /etc/nginx/sites-available/seguros-alonzo /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx

# Obtener certificado SSL gratuito
sudo certbot --nginx -d tu-dominio.com -d www.tu-dominio.com
```

Certbot modifica automáticamente la configuración de Nginx para HTTPS. Verificar renovación automática:
```bash
sudo certbot renew --dry-run
```

---

## 7. Configurar el Webhook de WhatsApp en Meta

Una vez que el servidor esté corriendo con HTTPS:

1. Ve a [Meta Business Manager](https://business.facebook.com) → WhatsApp → Configuration → Webhook
2. En **Callback URL** pon: `https://tu-dominio.com/api/webhook/whatsapp`
3. En **Verify Token** pon el mismo valor que configuraste en `WhatsApp:WebhookVerifyToken`
4. Haz clic en **Verify and Save**
5. Suscríbete a los eventos: `messages`, `message_status_updates`

---

## 8. Crear la plantilla de WhatsApp en Meta

Para que el sistema pueda enviar notificaciones automáticas (renovaciones, pagos, reclamos):

1. Ve a Meta Business Manager → WhatsApp → **Manage Templates**
2. Haz clic en **Create Template**
3. Configura:
   - **Category:** Utility
   - **Language:** Spanish (es)
   - **Name:** (ej: `notificacion_seguros`) — sin espacios, solo minúsculas y guiones bajos
4. En el cuerpo del mensaje escribe: `{{1}}` (el sistema enviará el mensaje completo como ese parámetro)
5. Envía para aprobación (24-48 horas)
6. Una vez aprobada, ve al sistema → Configuración → WhatsApp → escribe el nombre exacto en **Plantilla de mensajes**

---

## 9. Primer inicio y usuario administrador

Al iniciar por primera vez el sistema crea automáticamente el schema de la base de datos.

Para crear el primer usuario administrador, conectarse directamente a la base de datos:

```sql
-- El password debe ser un hash BCrypt del password real.
-- Generarlo en: https://bcrypt-generator.com/ (cost 10)
INSERT INTO users (nombre, email, password_hash, rol, activo)
VALUES ('Administrador', 'admin@tucorreo.com', '$2a$10$HASH_GENERADO', 'ADMIN', 1);
```

O usar el endpoint de creación de usuarios desde la UI una vez que el primer admin esté activo.

---

## 10. Verificación post-despliegue

Checklist antes de dar el sistema por operativo:

- [ ] `https://tu-dominio.com` carga el login sin errores
- [ ] Inicio de sesión funciona
- [ ] Dashboard carga datos de la base de datos
- [ ] Módulo de clientes y pólizas funciona
- [ ] WhatsApp activo y prueba de mensaje enviada exitosamente
- [ ] Webhook de WhatsApp verificado en Meta (✓ verde en Meta BM)
- [ ] Correo IMAP conectado (revisar logs del servicio)
- [ ] `sudo certbot renew --dry-run` sin errores

---

## Variables de entorno alternativas

En lugar de editar `appsettings.json` en el servidor, puedes usar variables de entorno en el servicio systemd. En `/etc/systemd/system/seguros-alonzo.service` agrega:

```ini
[Service]
Environment=ConnectionStrings__Default=Server=localhost;Port=3306;Database=reclamos_auto;User ID=seguros_user;Password=TU_PASSWORD;SslMode=None;
Environment=Email__Password=tu_app_password_gmail
Environment=WhatsApp__AccessToken=tu_token_meta
Environment=WhatsApp__WebhookVerifyToken=tu_token_secreto
```

> Nota: en variables de entorno, los `:` de JSON se reemplazan por `__` (doble guión bajo).

---

## Actualizar el sistema

```bash
# En la máquina de desarrollo:
dotnet publish -c Release -o ./publish
./ClientApp/node_modules/.bin/vite build --config ClientApp/vite.config.ts

# Subir al servidor:
rsync -avz ./publish/ usuario@tu-servidor:/opt/seguros-alonzo/

# Reiniciar el servicio:
sudo systemctl restart seguros-alonzo
```

---

## Estructura del proyecto

```
ReclamosWhatsApp/
├── Controllers/Api/          # Endpoints REST
│   ├── AuthApiController.cs
│   ├── CarteraApiController.cs
│   ├── DashboardApiController.cs
│   ├── WhatsAppWebhookController.cs  # Webhook Meta
│   └── ...
├── Data/                     # Repositorios Dapper + MariaDB
├── Models/                   # Clases de dominio
├── Services/                 # Lógica de negocio
│   ├── WhatsAppService.cs    # Envío WhatsApp (texto libre + plantillas)
│   ├── AutomaticWhatsAppService.cs  # Automatizaciones
│   └── ...
├── ClientApp/                # React 19 + Vite
│   ├── src/views/            # Pantallas
│   ├── src/components/       # Componentes reutilizables
│   └── src/api/              # Clientes HTTP
├── storage/                  # Documentos subidos (NO en git)
├── appsettings.json          # Configuración local (NO en git)
├── appsettings.example.json  # Plantilla sin secretos (en git)
└── DEPLOYMENT.md             # Este archivo
```

---

## Soporte y logs

- Logs del sistema: `sudo journalctl -u seguros-alonzo -f`
- Logs de Nginx: `/var/log/nginx/error.log`
- Auditoría de acciones: módulo **Auditoría** dentro del sistema
- Errores WhatsApp: módulo **Auditoría** → filtrar por "WHATSAPP"
