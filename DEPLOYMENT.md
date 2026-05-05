# Guía de Despliegue — Sistema de Correduría Alonzo
## Plataforma: Hetzner Cloud

---

## Servidor recomendado en Hetzner

| Opción | Tipo | CPU | RAM | Disco | Precio aprox. |
|--------|------|-----|-----|-------|---------------|
| ✅ Recomendado | **CX22** | 2 vCPU | 4 GB | 40 GB SSD | ~€4.35/mes |
| Mínimo viable | CX11 | 2 vCPU | 2 GB | 20 GB SSD | ~€3.29/mes |
| Crecimiento futuro | CX32 | 4 vCPU | 8 GB | 80 GB SSD | ~€8.27/mes |

**Sistema operativo:** Ubuntu 22.04 LTS  
**Datacenter:** cualquiera (Nuremberg, Helsinki o Falkenstein)  
**Red:** IPv4 habilitada ✓

---

## PASO 1 — Crear el servidor en Hetzner

1. Entra a [console.hetzner.cloud](https://console.hetzner.cloud)
2. **New Server** → elige:
   - Location: Nuremberg (NBG1) o el más cercano
   - Image: **Ubuntu 22.04**
   - Type: **CX22**
   - SSH Key: sube tu clave pública (o usa contraseña)
   - Server name: `seguros-alonzo`
3. Clic en **Create & Buy now**
4. Anota la **IP pública** que asigna (ej: `65.21.xxx.xxx`)

---

## PASO 2 — Apuntar tu dominio al servidor

En el panel de tu proveedor de DNS (Namecheap, GoDaddy, Cloudflare, etc.):

```
Tipo    Nombre    Valor
A       @         65.21.xxx.xxx      (tu IP de Hetzner)
A       www       65.21.xxx.xxx
```

> Los cambios de DNS pueden tardar entre 5 minutos y 48 horas en propagarse.
> Verifica en: https://dnschecker.org

---

## PASO 3 — Conectarse al servidor

```bash
ssh root@65.21.xxx.xxx
```

Primero actualizar el sistema:
```bash
apt update && apt upgrade -y
```

---

## PASO 4 — Instalar .NET 8 Runtime

```bash
# Agregar repositorio de Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Instalar runtime de ASP.NET Core 8
apt-get update
apt-get install -y aspnetcore-runtime-8.0

# Verificar
dotnet --list-runtimes
# Debe aparecer: Microsoft.AspNetCore.App 8.0.x
```

---

## PASO 5 — Instalar MariaDB

```bash
apt install mariadb-server -y
systemctl enable mariadb
systemctl start mariadb

# Asegurar la instalación (seguir el asistente: contraseña root, eliminar anónimos, etc.)
mysql_secure_installation
```

Crear base de datos y usuario de la aplicación:
```bash
mysql -u root -p
```

```sql
CREATE DATABASE reclamos_auto CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'seguros_user'@'localhost' IDENTIFIED BY 'PON_UNA_PASSWORD_SEGURA_AQUI';
GRANT ALL PRIVILEGES ON reclamos_auto.* TO 'seguros_user'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

---

## PASO 6 — Subir la aplicación al servidor

**En tu máquina Windows** (desde la raíz del proyecto):

```powershell
# Compilar el frontend
.\ClientApp\node_modules\.bin\vite build --config ClientApp/vite.config.ts

# Publicar el backend
& "C:\Dotnet\dotnet.exe" publish -c Release -o ./publish

# Subir todo al servidor (necesitas rsync o usa WinSCP/FileZilla)
# Con rsync desde Git Bash o WSL:
rsync -avz --exclude='appsettings.json' ./publish/ root@65.21.xxx.xxx:/opt/seguros-alonzo/
```

> **Alternativa con WinSCP:** conectarse por SFTP a la IP del servidor y arrastrar la carpeta `publish/` a `/opt/seguros-alonzo/`

Crear carpeta de almacenamiento:
```bash
mkdir -p /opt/seguros-alonzo/storage
chmod 755 /opt/seguros-alonzo/storage
```

---

## PASO 7 — Configurar `appsettings.json` en el servidor

```bash
cd /opt/seguros-alonzo
cp appsettings.example.json appsettings.json
nano appsettings.json
```

Editar con los valores reales:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=reclamos_auto;User ID=seguros_user;Password=PON_UNA_PASSWORD_SEGURA_AQUI;SslMode=None;"
  },
  "Admin": {
    "WhatsAppNumber": "504XXXXXXXXX"
  },
  "Email": {
    "Enabled": true,
    "Host": "imap.gmail.com",
    "Port": 993,
    "User": "reclamos@tucorreo.com",
    "Password": "xxxx xxxx xxxx xxxx",
    "UseSsl": true,
    "Mailbox": "INBOX",
    "MarkAsRead": false,
    "LookbackHours": 24
  },
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "User": "produccion@tucorreo.com",
    "Password": "xxxx xxxx xxxx xxxx",
    "From": "produccion@tucorreo.com",
    "FromName": "Seguros Alonzo"
  },
  "WhatsApp": {
    "Enabled": false,
    "GraphVersion": "v18.0",
    "PhoneNumberId": "TU_PHONE_NUMBER_ID",
    "AccessToken": "TU_ACCESS_TOKEN_META",
    "WebhookVerifyToken": "cualquier_texto_secreto_que_tu_elijas",
    "TemplateName": "",
    "LanguageCode": "es"
  },
  "Worker": {
    "Enabled": true,
    "IntervalSeconds": 30
  },
  "Documentos": {
    "CarpetaBase": "storage",
    "TamanoMaximoBytes": 5242880,
    "ExtensionesPermitidas": ".pdf,.jpg,.jpeg,.png,.webp,.txt,.doc,.docx,.xls,.xlsx"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "tu-dominio.com"
}
```

| Campo | Dónde encontrarlo |
|-------|-------------------|
| `ConnectionStrings:Default` | El usuario/password que creaste en MariaDB |
| `Email:Password` | Gmail → Cuenta → Seguridad → Contraseñas de aplicaciones |
| `Smtp:Password` | Contraseña de aplicación de la cuenta que enviará documentos |
| `WhatsApp:PhoneNumberId` | Meta Business Manager → WhatsApp → API Setup |
| `WhatsApp:AccessToken` | Meta Business Manager → WhatsApp → API Setup → Token permanente |
| `WhatsApp:WebhookVerifyToken` | Inventatelo tú (ej: `seguros_webhook_2024_xyz`) |

> `appsettings.json` **no se sube a git**. Solo existe en el servidor.

---

## PASO 8 — Configurar como servicio systemd

```bash
nano /etc/systemd/system/seguros-alonzo.service
```

Pegar este contenido:
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

Cambiar permisos de la carpeta:
```bash
chown -R www-data:www-data /opt/seguros-alonzo

# Activar e iniciar el servicio
systemctl daemon-reload
systemctl enable seguros-alonzo
systemctl start seguros-alonzo
systemctl status seguros-alonzo
```

Ver si arrancó bien:
```bash
journalctl -u seguros-alonzo -f
# Debes ver: "Now listening on: http://localhost:5000"
```

---

## PASO 9 — Instalar Nginx y SSL (HTTPS gratis con Let's Encrypt)

```bash
apt install nginx certbot python3-certbot-nginx -y
```

Crear la configuración del sitio:
```bash
nano /etc/nginx/sites-available/seguros-alonzo
```

```nginx
server {
    server_name tu-dominio.com www.tu-dominio.com;

    client_max_body_size 10M;

    location / {
        proxy_pass          http://localhost:5000;
        proxy_http_version  1.1;
        proxy_set_header    Upgrade $http_upgrade;
        proxy_set_header    Connection keep-alive;
        proxy_set_header    Host $host;
        proxy_set_header    X-Real-IP $remote_addr;
        proxy_set_header    X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;
        proxy_cache_bypass  $http_upgrade;
        proxy_read_timeout  300s;
        proxy_connect_timeout 75s;
    }
}
```

Activar y obtener certificado SSL:
```bash
ln -s /etc/nginx/sites-available/seguros-alonzo /etc/nginx/sites-enabled/
nginx -t
systemctl reload nginx

# Obtener certificado SSL gratuito (reemplaza con tu dominio real)
certbot --nginx -d tu-dominio.com -d www.tu-dominio.com
```

Certbot configura HTTPS automáticamente. Verificar renovación automática:
```bash
certbot renew --dry-run
# Debe decir: "Congratulations, all simulated renewals succeeded"
```

---

## PASO 10 — Crear el primer usuario administrador

El sistema crea el schema de la base de datos automáticamente al iniciar.

Generar un hash BCrypt de tu contraseña en: **https://bcrypt-generator.com** (Cost: 10)

Luego insertarlo en la BD:
```bash
mysql -u root -p reclamos_auto
```

```sql
INSERT INTO users (nombre, email, password_hash, rol, activo)
VALUES (
  'Administrador',
  'admin@tucorreo.com',
  '$2a$10$PEGA_EL_HASH_GENERADO_AQUI',
  'ADMIN',
  1
);
EXIT;
```

Prueba entrando a `https://tu-dominio.com` con ese email y contraseña.

---

## PASO 11 — Configurar WhatsApp Webhook en Meta

Con el servidor corriendo en HTTPS:

1. Ir a [developers.facebook.com](https://developers.facebook.com) → Tu App → WhatsApp → Configuration
2. En **Webhook**:
   - **Callback URL:** `https://tu-dominio.com/api/webhook/whatsapp`
   - **Verify Token:** el mismo valor que pusiste en `WhatsApp:WebhookVerifyToken`
3. Clic en **Verify and Save** — debe aparecer ✓ verde
4. Suscribirse a: **messages** y **message_status_updates**

---

## PASO 12 — Crear plantilla de WhatsApp (para mensajes automáticos)

Sin plantilla aprobada, los mensajes automáticos solo funcionan si el cliente te escribió primero en las últimas 24h.

1. Meta Business Manager → WhatsApp → **Manage Templates** → Create Template
2. Configurar:
   - Category: **Utility**
   - Language: **Spanish (es)**
   - Name: `notificacion_seguros` (sin espacios, solo minúsculas y guiones bajos)
3. Cuerpo del mensaje: `{{1}}`
   - Ejemplo: *"{{1}}"* — el sistema pone el texto completo ahí
4. Enviar para revisión (Meta tarda 24-48h en aprobar)
5. Una vez aprobada: sistema → **Configuración → WhatsApp → Plantilla de mensajes** → escribir `notificacion_seguros`

---

## Actualizar el sistema en el futuro

En tu máquina Windows:
```powershell
# Compilar
.\ClientApp\node_modules\.bin\vite build --config ClientApp/vite.config.ts
& "C:\Dotnet\dotnet.exe" publish -c Release -o ./publish

# Subir (desde Git Bash o WSL)
rsync -avz --exclude='appsettings.json' ./publish/ root@65.21.xxx.xxx:/opt/seguros-alonzo/
```

En el servidor:
```bash
systemctl restart seguros-alonzo
journalctl -u seguros-alonzo -f   # verificar que arrancó bien
```

---

## Firewall en Hetzner (opcional pero recomendado)

En el panel de Hetzner → Firewalls → Create Firewall:

| Dirección | Protocolo | Puerto | Descripción |
|-----------|-----------|--------|-------------|
| Inbound | TCP | 22 | SSH |
| Inbound | TCP | 80 | HTTP (redirige a HTTPS) |
| Inbound | TCP | 443 | HTTPS |
| Outbound | TCP+UDP | any | Todo el tráfico saliente |

Aplicar el firewall al servidor `seguros-alonzo`.

---

## Backups de la base de datos

Script de backup diario automático:
```bash
nano /opt/backup-bd.sh
```

```bash
#!/bin/bash
FECHA=$(date +%Y%m%d_%H%M)
DESTINO="/opt/backups"
mkdir -p $DESTINO
mysqldump -u seguros_user -p'PON_PASSWORD_AQUI' reclamos_auto | gzip > $DESTINO/reclamos_auto_$FECHA.sql.gz
# Mantener solo los últimos 30 backups
ls -t $DESTINO/*.sql.gz | tail -n +31 | xargs rm -f
```

```bash
chmod +x /opt/backup-bd.sh

# Programar para ejecutar cada día a las 2am
crontab -e
# Agregar esta línea:
0 2 * * * /opt/backup-bd.sh
```

---

## Checklist final antes de dar por operativo

- [ ] `https://tu-dominio.com` carga el login sin errores de certificado
- [ ] Inicio de sesión con el admin funciona
- [ ] Dashboard muestra datos (conectado a MariaDB)
- [ ] Módulo de Clientes y Pólizas funciona
- [ ] Correo IMAP conectado: `journalctl -u seguros-alonzo | grep -i email`
- [ ] WhatsApp: enviar prueba desde **Configuración → WhatsApp → Prueba manual**
- [ ] Webhook de WhatsApp verificado (✓ verde en Meta)
- [ ] `certbot renew --dry-run` sin errores
- [ ] Backup manual ejecutado y archivo `.sql.gz` generado

---

## Comandos útiles en el servidor

```bash
# Ver estado del sistema
systemctl status seguros-alonzo

# Ver logs en tiempo real
journalctl -u seguros-alonzo -f

# Reiniciar la aplicación
systemctl restart seguros-alonzo

# Ver errores de Nginx
cat /var/log/nginx/error.log | tail -20

# Conectarse a la BD
mysql -u seguros_user -p reclamos_auto

# Ver espacio en disco
df -h

# Ver uso de RAM
free -h

# Ver CPU y procesos
htop
```

---

## Estructura del proyecto

```
ReclamosWhatsApp/
├── Controllers/Api/            # Endpoints REST
│   ├── AuthApiController.cs
│   ├── CarteraApiController.cs
│   ├── DashboardApiController.cs
│   ├── WhatsAppWebhookController.cs   # Webhook Meta (GET verify + POST callbacks)
│   ├── ReclamosApiController.cs
│   ├── PagosApiController.cs
│   ├── RecordatoriosApiController.cs
│   ├── GastosApiController.cs
│   └── ...
├── Data/                       # Repositorios con Dapper + MariaDB
│   ├── CarteraRepository.cs   # Clientes, pólizas, dashboard
│   ├── PagoRepository.cs      # Cuotas y pagos
│   ├── WhatsAppEnvioLogRepository.cs
│   └── ...
├── Models/                     # Clases de dominio
├── Services/                   # Lógica de negocio
│   ├── WhatsAppService.cs     # Envío (plantillas Meta + texto libre)
│   ├── AutomaticWhatsAppService.cs   # Automatizaciones de envío
│   ├── EmailReaderService.cs  # Lectura de reclamos por IMAP
│   └── ...
├── Security/
│   └── Permissions.cs         # Todos los permisos del sistema
├── ClientApp/                  # React 19 + Vite + TypeScript
│   ├── src/views/             # Pantallas principales
│   ├── src/components/        # Componentes reutilizables
│   │   └── ErrorBoundary.tsx  # Protección contra crashes de UI
│   └── src/api/               # Clientes HTTP con manejo de errores
├── storage/                    # Documentos subidos (NO en git)
├── appsettings.json            # Config local con secretos (NO en git)
├── appsettings.example.json    # Plantilla sin secretos (en git)
└── DEPLOYMENT.md               # Esta guía
```
