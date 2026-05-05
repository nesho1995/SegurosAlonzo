using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class AppSettingsRepository
{
    private readonly DbConnectionFactory _factory;

    public AppSettingsRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<CorreoExtractorConfig> GetCorreoExtractorConfigAsync()
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'correo_extractor';";

        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value);

        string Get(string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : CorreoExtractorConfig.DefaultValues[key];

        return new CorreoExtractorConfig
        {
            SubjectPattern = Get(nameof(CorreoExtractorConfig.SubjectPattern)),
            AseguradoPattern = Get(nameof(CorreoExtractorConfig.AseguradoPattern)),
            PolizaPattern = Get(nameof(CorreoExtractorConfig.PolizaPattern)),
            CaracteristicasPattern = Get(nameof(CorreoExtractorConfig.CaracteristicasPattern)),
            ConductorPattern = Get(nameof(CorreoExtractorConfig.ConductorPattern)),
            CelularPattern = Get(nameof(CorreoExtractorConfig.CelularPattern)),
            FechaPattern = Get(nameof(CorreoExtractorConfig.FechaPattern)),
            LugarPattern = Get(nameof(CorreoExtractorConfig.LugarPattern))
        };
    }

    public async Task SaveCorreoExtractorConfigAsync(CorreoExtractorConfig config)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('correo_extractor', @Key, @Value)
            ON DUPLICATE KEY UPDATE
                setting_value = VALUES(setting_value),
                updated_at = CURRENT_TIMESTAMP;";

        var values = new Dictionary<string, string>
        {
            [nameof(config.SubjectPattern)] = config.SubjectPattern,
            [nameof(config.AseguradoPattern)] = config.AseguradoPattern,
            [nameof(config.PolizaPattern)] = config.PolizaPattern,
            [nameof(config.CaracteristicasPattern)] = config.CaracteristicasPattern,
            [nameof(config.ConductorPattern)] = config.ConductorPattern,
            [nameof(config.CelularPattern)] = config.CelularPattern,
            [nameof(config.FechaPattern)] = config.FechaPattern,
            [nameof(config.LugarPattern)] = config.LugarPattern
        };

        foreach (var item in values)
        {
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
        }
    }

    public async Task<ExtractorAdvancedConfig> GetExtractorAdvancedConfigAsync()
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'extractor_avanzado';";

        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value);

        string Get(string key, string fallback) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        var defaults = new ExtractorAdvancedConfig();
        return new ExtractorAdvancedConfig
        {
            RemitentesPermitidos = Get(nameof(defaults.RemitentesPermitidos), defaults.RemitentesPermitidos),
            PalabrasClaveAsunto = Get(nameof(defaults.PalabrasClaveAsunto), defaults.PalabrasClaveAsunto),
            AseguradorasReglas = Get(nameof(defaults.AseguradorasReglas), defaults.AseguradorasReglas),
            CamposObligatorios = Get(nameof(defaults.CamposObligatorios), defaults.CamposObligatorios),
            PlantillaWhatsApp = Get(nameof(defaults.PlantillaWhatsApp), defaults.PlantillaWhatsApp)
        };
    }

    public async Task SaveExtractorAdvancedConfigAsync(ExtractorAdvancedConfig config)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('extractor_avanzado', @Key, @Value)
            ON DUPLICATE KEY UPDATE
                setting_value = VALUES(setting_value),
                updated_at = CURRENT_TIMESTAMP;";

        var values = new Dictionary<string, string>
        {
            [nameof(config.RemitentesPermitidos)] = config.RemitentesPermitidos ?? "",
            [nameof(config.PalabrasClaveAsunto)] = config.PalabrasClaveAsunto ?? "",
            [nameof(config.AseguradorasReglas)] = config.AseguradorasReglas ?? "",
            [nameof(config.CamposObligatorios)] = config.CamposObligatorios ?? "",
            [nameof(config.PlantillaWhatsApp)] = config.PlantillaWhatsApp ?? ""
        };

        foreach (var item in values)
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
    }

    public async Task<EnvioAutomaticoConfig> GetEnvioAutomaticoConfigAsync()
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'envios_automaticos';";

        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value);
        var defaults = new EnvioAutomaticoConfig();

        string Get(string key, string fallback) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        bool GetBool(string key, bool fallback) =>
            bool.TryParse(Get(key, fallback ? "true" : "false"), out var value) ? value : fallback;

        string GetDias(string key, string fallback, int max)
        {
            var raw = Get(key, fallback);
            var dias = raw
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var value) ? Math.Clamp(value, 0, max) : -1)
                .Where(x => x >= 0)
                .Distinct()
                .OrderByDescending(x => x)
                .ToArray();
            return dias.Length == 0 ? fallback : string.Join(",", dias);
        }

        return new EnvioAutomaticoConfig
        {
            AutoEnviarReclamos = GetBool(nameof(defaults.AutoEnviarReclamos), defaults.AutoEnviarReclamos),
            AutoEnviarRecordatoriosPago = GetBool(nameof(defaults.AutoEnviarRecordatoriosPago), defaults.AutoEnviarRecordatoriosPago),
            AutoEnviarRecordatoriosPoliza = GetBool(nameof(defaults.AutoEnviarRecordatoriosPoliza), defaults.AutoEnviarRecordatoriosPoliza),
            DiasAntesVencimientoCuota = GetDias(nameof(defaults.DiasAntesVencimientoCuota), defaults.DiasAntesVencimientoCuota, 90),
            DiasDespuesCuotaVencida = GetDias(nameof(defaults.DiasDespuesCuotaVencida), defaults.DiasDespuesCuotaVencida, 90),
            DiasAntesVencimientoPoliza = GetDias(nameof(defaults.DiasAntesVencimientoPoliza), defaults.DiasAntesVencimientoPoliza, 180),
            PlantillaPagoProximo = Get(nameof(defaults.PlantillaPagoProximo), defaults.PlantillaPagoProximo),
            PlantillaPagoVencido = Get(nameof(defaults.PlantillaPagoVencido), defaults.PlantillaPagoVencido),
            PlantillaPolizaPorVencer = Get(nameof(defaults.PlantillaPolizaPorVencer), defaults.PlantillaPolizaPorVencer),
            PlantillaPolizaVencida = Get(nameof(defaults.PlantillaPolizaVencida), defaults.PlantillaPolizaVencida),
            PlantillaReclamo = Get(nameof(defaults.PlantillaReclamo), defaults.PlantillaReclamo)
        };
    }

    public async Task SaveEnvioAutomaticoConfigAsync(EnvioAutomaticoConfig config)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('envios_automaticos', @Key, @Value)
            ON DUPLICATE KEY UPDATE
                setting_value = VALUES(setting_value),
                updated_at = CURRENT_TIMESTAMP;";

        var values = new Dictionary<string, string>
        {
            [nameof(config.AutoEnviarReclamos)] = config.AutoEnviarReclamos.ToString().ToLowerInvariant(),
            [nameof(config.AutoEnviarRecordatoriosPago)] = config.AutoEnviarRecordatoriosPago.ToString().ToLowerInvariant(),
            [nameof(config.AutoEnviarRecordatoriosPoliza)] = config.AutoEnviarRecordatoriosPoliza.ToString().ToLowerInvariant(),
            [nameof(config.DiasAntesVencimientoCuota)] = NormalizeDias(config.DiasAntesVencimientoCuota, 90, "7,3,1"),
            [nameof(config.DiasDespuesCuotaVencida)] = NormalizeDias(config.DiasDespuesCuotaVencida, 90, "1,3,7,15"),
            [nameof(config.DiasAntesVencimientoPoliza)] = NormalizeDias(config.DiasAntesVencimientoPoliza, 180, "30,15,7"),
            [nameof(config.PlantillaPagoProximo)] = config.PlantillaPagoProximo ?? "",
            [nameof(config.PlantillaPagoVencido)] = config.PlantillaPagoVencido ?? "",
            [nameof(config.PlantillaPolizaPorVencer)] = config.PlantillaPolizaPorVencer ?? "",
            [nameof(config.PlantillaPolizaVencida)] = config.PlantillaPolizaVencida ?? "",
            [nameof(config.PlantillaReclamo)] = config.PlantillaReclamo ?? ""
        };

        foreach (var item in values)
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
    }

    public async Task<WhatsAppConfig> GetWhatsAppConfigAsync(IConfiguration configuration, bool includeSecret = false)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'whatsapp';";

        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        string Get(string key, string fallback)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        bool GetBool(string key, bool fallback)
            => bool.TryParse(Get(key, fallback ? "true" : "false"), out var parsed) ? parsed : fallback;

        var token = Get(nameof(WhatsAppConfig.AccessToken), configuration["WhatsApp:AccessToken"] ?? "");
        var webhookToken = Get(nameof(WhatsAppConfig.WebhookVerifyToken), configuration["WhatsApp:WebhookVerifyToken"] ?? "");
        return new WhatsAppConfig
        {
            Enabled = GetBool(nameof(WhatsAppConfig.Enabled), configuration.GetValue<bool>("WhatsApp:Enabled")),
            GraphVersion = Get(nameof(WhatsAppConfig.GraphVersion), configuration["WhatsApp:GraphVersion"] ?? "v18.0"),
            PhoneNumberId = Get(nameof(WhatsAppConfig.PhoneNumberId), configuration["WhatsApp:PhoneNumberId"] ?? ""),
            AccessToken = includeSecret ? token : "",
            AccessTokenMasked = MaskSecret(token),
            TemplateName = Get(nameof(WhatsAppConfig.TemplateName), configuration["WhatsApp:TemplateName"] ?? ""),
            LanguageCode = Get(nameof(WhatsAppConfig.LanguageCode), configuration["WhatsApp:LanguageCode"] ?? "es"),
            AdminWhatsAppNumber = Get(nameof(WhatsAppConfig.AdminWhatsAppNumber), configuration["Admin:WhatsAppNumber"] ?? ""),
            WebhookVerifyToken = includeSecret ? webhookToken : "",
            WebhookVerifyTokenMasked = MaskSecret(webhookToken),
        };
    }

    public async Task SaveWhatsAppConfigAsync(WhatsAppConfig config, IConfiguration configuration)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('whatsapp', @Key, @Value)
            ON DUPLICATE KEY UPDATE
                setting_value = VALUES(setting_value),
                updated_at = CURRENT_TIMESTAMP;";

        var current = await GetWhatsAppConfigAsync(configuration, includeSecret: true);
        var token = string.IsNullOrWhiteSpace(config.AccessToken) || IsMaskedSecret(config.AccessToken)
            ? current.AccessToken
            : config.AccessToken.Trim();

        var currentWebhookToken = string.IsNullOrWhiteSpace(config.WebhookVerifyToken) || IsMaskedSecret(config.WebhookVerifyToken)
            ? current.WebhookVerifyToken
            : config.WebhookVerifyToken.Trim();

        var values = new Dictionary<string, string>
        {
            [nameof(config.Enabled)] = config.Enabled.ToString().ToLowerInvariant(),
            [nameof(config.GraphVersion)] = string.IsNullOrWhiteSpace(config.GraphVersion) ? "v18.0" : config.GraphVersion.Trim(),
            [nameof(config.PhoneNumberId)] = config.PhoneNumberId?.Trim() ?? "",
            [nameof(config.AccessToken)] = token,
            [nameof(config.TemplateName)] = config.TemplateName?.Trim() ?? "",
            [nameof(config.LanguageCode)] = string.IsNullOrWhiteSpace(config.LanguageCode) ? "es" : config.LanguageCode.Trim(),
            [nameof(config.AdminWhatsAppNumber)] = config.AdminWhatsAppNumber?.Trim() ?? "",
            [nameof(config.WebhookVerifyToken)] = currentWebhookToken,
        };

        foreach (var item in values)
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
    }

    private static string NormalizeDias(string? raw, int max, string fallback)
    {
        var dias = (raw ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var value) ? Math.Clamp(value, 0, max) : -1)
            .Where(x => x >= 0)
            .Distinct()
            .OrderByDescending(x => x)
            .ToArray();
        return dias.Length == 0 ? fallback : string.Join(",", dias);
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
            return "********";

        return $"{trimmed[..4]}********{trimmed[^4..]}";
    }

    private static bool IsMaskedSecret(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 && trimmed.All(ch => ch == '*');
    }

    public async Task<ReclamoCorreoConfig> GetReclamoCorreoConfigAsync(IConfiguration configuration)
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'reclamos_correo';";
        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        string Get(string key, string fallback)
            => values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
        bool GetBool(string key, bool fallback)
            => bool.TryParse(Get(key, fallback ? "true" : "false"), out var parsed) ? parsed : fallback;
        int GetInt(string key, int fallback)
            => int.TryParse(Get(key, fallback.ToString()), out var parsed) ? parsed : fallback;

        return new ReclamoCorreoConfig
        {
            EmailEnabled = GetBool(nameof(ReclamoCorreoConfig.EmailEnabled), configuration.GetValue<bool>("Email:Enabled")),
            WorkerEnabled = GetBool(nameof(ReclamoCorreoConfig.WorkerEnabled), configuration.GetValue<bool>("Worker:Enabled")),
            Mailbox = Get(nameof(ReclamoCorreoConfig.Mailbox), configuration["Email:Mailbox"] ?? "INBOX"),
            MarkAsRead = GetBool(nameof(ReclamoCorreoConfig.MarkAsRead), configuration.GetValue<bool>("Email:MarkAsRead")),
            LookbackHours = Math.Clamp(GetInt(nameof(ReclamoCorreoConfig.LookbackHours), configuration.GetValue<int?>("Email:LookbackHours") ?? 24), 1, 24 * 30),
            Host = Get(nameof(ReclamoCorreoConfig.Host), configuration["Email:Host"] ?? ""),
            Port = GetInt(nameof(ReclamoCorreoConfig.Port), configuration.GetValue<int?>("Email:Port") ?? 993),
            UseSsl = GetBool(nameof(ReclamoCorreoConfig.UseSsl), configuration.GetValue<bool?>("Email:UseSsl") ?? true),
            Username = Get(nameof(ReclamoCorreoConfig.Username), configuration["Email:User"] ?? ""),
            Password = Get(nameof(ReclamoCorreoConfig.Password), configuration["Email:Password"] ?? "")
        };
    }

    public async Task SaveReclamoCorreoConfigAsync(ReclamoCorreoConfig config)
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('reclamos_correo', @Key, @Value)
            ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value), updated_at = CURRENT_TIMESTAMP;";
        var values = new Dictionary<string, string>
        {
            [nameof(config.EmailEnabled)] = config.EmailEnabled.ToString().ToLowerInvariant(),
            [nameof(config.WorkerEnabled)] = config.WorkerEnabled.ToString().ToLowerInvariant(),
            [nameof(config.Mailbox)] = config.Mailbox ?? "INBOX",
            [nameof(config.MarkAsRead)] = config.MarkAsRead.ToString().ToLowerInvariant(),
            [nameof(config.LookbackHours)] = Math.Clamp(config.LookbackHours, 1, 24 * 30).ToString(),
            [nameof(config.Host)] = config.Host ?? "",
            [nameof(config.Port)] = (config.Port <= 0 ? 993 : config.Port).ToString(),
            [nameof(config.UseSsl)] = config.UseSsl.ToString().ToLowerInvariant(),
            [nameof(config.Username)] = config.Username ?? "",
            [nameof(config.Password)] = config.Password ?? ""
        };
        foreach (var item in values)
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
    }

    public async Task<ReclamoWorkerEstado> GetReclamoWorkerEstadoAsync()
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT setting_key `Key`, setting_value `Value`
            FROM app_settings
            WHERE setting_group = 'reclamos_worker_estado';";
        var rows = await cn.QueryAsync<(string Key, string Value)>(sql);
        var values = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        DateTime? dt = null;
        if (values.TryGetValue("UltimaEjecucionUtc", out var rawDate) && DateTime.TryParse(rawDate, out var parsedDate))
            dt = parsedDate;
        int.TryParse(values.TryGetValue("CorreosEncontrados", out var found) ? found : "0", out var encontrados);
        int.TryParse(values.TryGetValue("CorreosProcesados", out var processed) ? processed : "0", out var procesados);
        return new ReclamoWorkerEstado
        {
            UltimaEjecucionUtc = dt,
            UltimoError = values.TryGetValue("UltimoError", out var error) ? error : null,
            CorreosEncontrados = encontrados,
            CorreosProcesados = procesados
        };
    }

    public async Task SaveReclamoWorkerEstadoAsync(ReclamoWorkerEstado estado)
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO app_settings (setting_group, setting_key, setting_value)
            VALUES ('reclamos_worker_estado', @Key, @Value)
            ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value), updated_at = CURRENT_TIMESTAMP;";
        var values = new Dictionary<string, string>
        {
            ["UltimaEjecucionUtc"] = (estado.UltimaEjecucionUtc ?? DateTime.UtcNow).ToString("o"),
            ["UltimoError"] = estado.UltimoError ?? "",
            ["CorreosEncontrados"] = estado.CorreosEncontrados.ToString(),
            ["CorreosProcesados"] = estado.CorreosProcesados.ToString()
        };
        foreach (var item in values)
            await cn.ExecuteAsync(sql, new { item.Key, item.Value });
    }
}
