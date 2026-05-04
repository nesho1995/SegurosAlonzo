using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class AutomationEngineService
{
    private readonly AutomationRepository _repo;
    private readonly AuditoriaService _auditoria;
    private readonly ILogger<AutomationEngineService> _logger;

    public AutomationEngineService(
        AutomationRepository repo,
        AuditoriaService auditoria,
        ILogger<AutomationEngineService> logger)
    {
        _repo = repo;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<AutomatizacionTestResult> EvaluarEventoAsync(
        string tipoEvento,
        string entidadTipo,
        int? entidadId,
        object datos,
        AutomationExecutionMode mode = AutomationExecutionMode.Real,
        int? empresaId = null)
    {
        var result = new AutomatizacionTestResult();
        var normalizedEvent = NormalizeEvent(tipoEvento);
        var normalizedEntity = NormalizeEntity(entidadTipo);
        var data = ToDictionary(datos);
        var rules = (await _repo.GetActiveByEventAsync(normalizedEvent, empresaId)).ToList();
        result.ReglasEvaluadas = rules.Count;

        foreach (var rule in rules)
        {
            try
            {
                if (!ConditionsMatch(rule, data))
                {
                    result.Mensajes.Add($"La regla '{rule.Nombre}' no coincide con los datos.");
                    continue;
                }

                result.ReglasCoincidentes++;

                if (mode == AutomationExecutionMode.Real && await _repo.HasSuccessfulExecutionAsync(rule.Id, normalizedEntity, entidadId))
                {
                    result.Mensajes.Add($"La regla '{rule.Nombre}' ya fue ejecutada para esta entidad.");
                    continue;
                }

                foreach (var action in rule.Acciones)
                {
                    var message = BuildActionMessage(action, rule, mode);
                    await RegisterExecutionAsync(rule.Id, normalizedEntity, entidadId, mode == AutomationExecutionMode.Test ? "PRUEBA" : "PREPARADA", message);
                    result.Mensajes.Add(message);
                }

                if (rule.Acciones.Count == 0)
                {
                    var message = $"La regla '{rule.Nombre}' coincide, pero no tiene acciones configuradas.";
                    await RegisterExecutionAsync(rule.Id, normalizedEntity, entidadId, "SIN_ACCIONES", message);
                    result.Mensajes.Add(message);
                }

                if (mode == AutomationExecutionMode.Real)
                    await _auditoria.RegistrarAsync("AUTOMATIZACION_EJECUTADA", normalizedEntity, entidadId, $"Regla ejecutada: {rule.Nombre}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando automatizacion {RuleId}", rule.Id);
                var message = $"No se pudo ejecutar la regla '{rule.Nombre}'.";
                await RegisterExecutionAsync(rule.Id, normalizedEntity, entidadId, "ERROR", message);
                result.Mensajes.Add(message);
                if (mode == AutomationExecutionMode.Real)
                    await _auditoria.RegistrarAsync("AUTOMATIZACION_ERROR", normalizedEntity, entidadId, message);
            }
        }

        return result;
    }

    private async Task RegisterExecutionAsync(int automationId, string entityType, int? entityId, string result, string message)
    {
        await _repo.InsertLogAsync(new AutomatizacionLog
        {
            AutomatizacionId = automationId,
            EntidadTipo = entityType,
            EntidadId = entityId,
            Resultado = result,
            Mensaje = message.Length > 1000 ? message[..1000] : message
        });
    }

    private static bool ConditionsMatch(Automatizacion rule, Dictionary<string, object?> data)
    {
        foreach (var condition in rule.Condiciones)
        {
            if (!data.TryGetValue(condition.Campo, out var actual))
                return false;

            if (!Compare(actual, condition.Valor, condition.Operador))
                return false;
        }

        return true;
    }

    private static bool Compare(object? actual, string? expected, string op)
    {
        var actualText = Convert.ToString(actual, CultureInfo.InvariantCulture)?.Trim() ?? "";
        var expectedText = expected?.Trim() ?? "";

        if (TryDecimal(actualText, out var actualNumber) && TryDecimal(expectedText, out var expectedNumber))
        {
            return op switch
            {
                "=" or "==" => actualNumber == expectedNumber,
                "!=" or "<>" => actualNumber != expectedNumber,
                ">" => actualNumber > expectedNumber,
                ">=" => actualNumber >= expectedNumber,
                "<" => actualNumber < expectedNumber,
                "<=" => actualNumber <= expectedNumber,
                _ => CompareText(actualText, expectedText, op)
            };
        }

        if (DateTime.TryParse(actualText, out var actualDate) && DateTime.TryParse(expectedText, out var expectedDate))
        {
            return op switch
            {
                "=" or "==" => actualDate.Date == expectedDate.Date,
                "!=" or "<>" => actualDate.Date != expectedDate.Date,
                ">" => actualDate.Date > expectedDate.Date,
                ">=" => actualDate.Date >= expectedDate.Date,
                "<" => actualDate.Date < expectedDate.Date,
                "<=" => actualDate.Date <= expectedDate.Date,
                _ => CompareText(actualText, expectedText, op)
            };
        }

        return CompareText(actualText, expectedText, op);
    }

    private static bool CompareText(string actual, string expected, string op)
    {
        return op switch
        {
            "=" or "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" or "<>" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contiene" or "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "no_contiene" or "not_contains" => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "existe" => !string.IsNullOrWhiteSpace(actual),
            "vacio" => string.IsNullOrWhiteSpace(actual),
            _ => false
        };
    }

    private static bool TryDecimal(string value, out decimal result)
    {
        value = value.Replace("L", "", StringComparison.OrdinalIgnoreCase).Replace(",", "").Trim();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static string BuildActionMessage(AutomatizacionAccion action, Automatizacion rule, AutomationExecutionMode mode)
    {
        var actionLabel = action.TipoAccion switch
        {
            "email" => "preparo correo",
            "whatsapp" => "preparo WhatsApp para revision",
            "actualizar_estado" => "preparo actualizacion de estado",
            "asignar_taller" => "preparo asignacion de taller",
            _ => $"preparo accion {action.TipoAccion}"
        };

        var prefix = mode == AutomationExecutionMode.Test ? "Prueba" : "Automatizacion";
        return $"{prefix}: la regla '{rule.Nombre}' {actionLabel}.";
    }

    private static Dictionary<string, object?> ToDictionary(object source)
    {
        if (source is Dictionary<string, object?> dictionary)
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(source);
        var result = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in result)
            normalized[item.Key] = item.Value;

        foreach (var prop in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            normalized[prop.Name] = prop.GetValue(source);

        return normalized;
    }

    private static string NormalizeEvent(string value) => (value ?? "").Trim().ToLowerInvariant();
    private static string NormalizeEntity(string value) => (value ?? "").Trim().ToUpperInvariant();
}
