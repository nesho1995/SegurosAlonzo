# Configuracion local segura

No guardar secretos reales en `appsettings.json`.

Variables de entorno soportadas:

```powershell
$env:ConnectionStrings__Default="server=localhost;port=3307;database=reclamos_auto;user=root;password=TU_PASSWORD;Allow User Variables=true;"
$env:Email__User="correo@empresa.com"
$env:Email__Password="app-password"
$env:WhatsApp__AccessToken="token"
$env:WhatsApp__PhoneNumberId="phone-id"
```

Para desarrollo sin envios reales:

```powershell
$env:Email__Enabled="false"
$env:WhatsApp__Enabled="false"
$env:Worker__Enabled="false"
```

Para produccion:

- Configurar secretos en variables de entorno, Docker secrets, Azure Key Vault, AWS Secrets Manager o equivalente.
- Usar HTTPS para que la cookie se emita con `Secure`.
- Rotar credenciales de correo y WhatsApp periodicamente.
- No publicar `appsettings.json` con claves reales.
