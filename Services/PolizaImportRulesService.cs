using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class PolizaImportRulesService
{
    public IReadOnlyList<HistoricalRuleValidationResult> RunValidationCases()
    {
        var results = new List<HistoricalRuleValidationResult>();

        var aCliente = new Cliente();
        var a = new Poliza { EstadoPago = "SIN_VALIDAR" };
        ApplyHistoricalRules(a, new HistoricalRulesContext { FormaPagoRaw = "CONTADO", Observaciones = "Amaury", Cliente = aCliente });
        results.Add(new("CasoA", a.EstadoPago == "PAGADO" && a.EstadoPolizaReal == "ACTIVA" && aCliente.ReferidoDetectado && aCliente.ReferidoPorNombre == "Amaury", $"Pago={a.EstadoPago}, EstadoPoliza={a.EstadoPolizaReal}, Referido={aCliente.ReferidoPorNombre}"));

        var b = new Poliza { EstadoPago = "SIN_VALIDAR" };
        ApplyHistoricalRules(b, new HistoricalRulesContext { FormaPagoRaw = "EXTRA", Observacion2 = "CANCELADA POR FALTA DE PAGO" });
        results.Add(new("CasoB", b.EstadoPago == "PAGADO" && b.EstadoPolizaReal == "CANCELADA", $"Pago={b.EstadoPago}, EstadoPoliza={b.EstadoPolizaReal}, Motivo={b.MotivoCancelacion}"));

        var c = new Poliza { EstadoPago = "SIN_VALIDAR" };
        ApplyHistoricalRules(c, new HistoricalRulesContext { FormaPagoRaw = "DEBITO", Observacion2 = "NO RENOVO" });
        results.Add(new("CasoC", c.EstadoPago == "EN_CUOTAS" && c.EstadoPolizaReal == "NO_RENOVADA", $"Pago={c.EstadoPago}, EstadoPoliza={c.EstadoPolizaReal}"));

        var d = new Poliza();
        ApplyHistoricalRules(d, new HistoricalRulesContext { RamoRaw = "AUTO" });
        results.Add(new("CasoD", d.RamoNormalizado == "AUTOS", $"Ramo={d.RamoNormalizado}"));

        var e = new Poliza();
        ApplyHistoricalRules(e, new HistoricalRulesContext { RamoRaw = "MAQUINARIA" });
        results.Add(new("CasoE", e.RamoNormalizado == "EQUIPO_MAQUINARIA", $"Ramo={e.RamoNormalizado}"));

        var f = new Poliza();
        ApplyHistoricalRules(f, new HistoricalRulesContext { EmisionRenovacionRaw = "RENOVACION" });
        results.Add(new("CasoF", f.TipoProceso == "RENOVACION", $"TipoProceso={f.TipoProceso}"));

        var g = new Poliza();
        ApplyHistoricalRules(g, new HistoricalRulesContext { Observacion2 = "CLIENTE VENDIO EL VEHICULO" });
        results.Add(new("CasoG", g.EstadoPolizaReal is "CANCELADA" or "NO_RENOVADA", $"EstadoPoliza={g.EstadoPolizaReal}, Motivo={g.MotivoCancelacion}"));

        return results;
    }

    public void ApplyHistoricalRules(Poliza poliza, HistoricalRulesContext context)
    {
        NormalizeBase(poliza, context);
        ApplyRamo(poliza, context);
        ApplyFormaPagoEstadoPago(poliza, context);
        ApplyTipoProceso(poliza, context);
        ApplyEstadoPoliza(poliza, context);
        ApplySumaAsegurada(poliza, context);
        ApplyReferido(context.Cliente, context.Observaciones, context.Observacion2);
        ApplyEstadoRevision(poliza);
    }

    private static void NormalizeBase(Poliza poliza, HistoricalRulesContext context)
    {
        poliza.Ramo = EmptyToNull(context.RamoRaw);
        poliza.FormaPago = EmptyToNull(context.FormaPagoRaw);
        poliza.EmisionRenovacion = EmptyToNull(context.EmisionRenovacionRaw);
        poliza.Observacion2 = EmptyToNull(context.Observacion2);
        poliza.Observaciones = EmptyToNull(context.Observaciones);
    }

    private static void ApplyRamo(Poliza poliza, HistoricalRulesContext context)
    {
        if (!CanUpdate(poliza.OrigenRamoNormalizado, context.AllowOverwriteAutomaticOnly))
            return;

        var normalizedRaw = NormalizeRaw(context.RamoRaw);
        var key = Regex.Replace(normalizedRaw, @"[^A-Z0-9]", "");
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUTO"] = "AUTOS", ["AUTOS"] = "AUTOS", ["VEHICULO"] = "AUTOS", ["VEHICULOAUTOMOTOR"] = "AUTOS",
            ["VIDA"] = "VIDA", ["VIDA2000"] = "VIDA2000", ["VIDAMEDICO"] = "VIDA_MEDICO",
            ["ACCIDENTESPERSONALES"] = "ACCIDENTES_PERSONALES", ["ACCIDENTEPERSONAL"] = "ACCIDENTES_PERSONALES", ["ACCIDENTESPERSONAL"] = "ACCIDENTES_PERSONALES", ["AP"] = "ACCIDENTES_PERSONALES",
            ["MAQUINARIA"] = "EQUIPO_MAQUINARIA", ["EQUIPOYMAQUINARIA"] = "EQUIPO_MAQUINARIA", ["EQUIPOMAQUINARIA"] = "EQUIPO_MAQUINARIA",
            ["MEDICO"] = "MEDICO", ["INCENDIO"] = "INCENDIO", ["INCENCIO"] = "INCENDIO", ["INCENCIDO"] = "INCENDIO",
            ["RC"] = "RC", ["RESPONSABILIDADCIVIL"] = "RC"
        };

        if (map.TryGetValue(key, out var ramo))
        {
            poliza.RamoNormalizado = ramo;
            poliza.OrigenRamoNormalizado = "REGLA_AUTOMATICA";
            return;
        }

        poliza.RamoNormalizado = "OTROS";
        poliza.OrigenRamoNormalizado = "REGLA_AUTOMATICA";
        MarkRevision(poliza, $"Ramo no reconocido: {context.RamoRaw?.Trim()}");
    }

    private static void ApplyFormaPagoEstadoPago(Poliza poliza, HistoricalRulesContext context)
    {
        if (!CanUpdate(poliza.OrigenEstadoPago, context.AllowOverwriteAutomaticOnly))
            return;

        var forma = NormalizeRaw(context.FormaPagoRaw);
        if (string.IsNullOrWhiteSpace(forma))
        {
            poliza.EstadoPago = "SIN_VALIDAR";
            poliza.MotivoEstadoPago = null;
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            MarkRevision(poliza, "Forma de pago no reconocida");
            return;
        }

        if (ContainsAny(forma, "CONTADO"))
        {
            poliza.EstadoPago = "PAGADO";
            poliza.MotivoEstadoPago = "Pago de contado";
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            return;
        }
        if (ContainsAny(forma, "EXTRAFINANCIAMIENTO", "EXTRA FINANCIAMIENTO", "EXTRA"))
        {
            poliza.EstadoPago = "PAGADO";
            poliza.MotivoEstadoPago = "Pagado por extrafinanciamiento";
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            return;
        }
        if (ContainsAny(forma, "DEBITO", "DEBITO AUTOMATICO"))
        {
            poliza.EstadoPago = "EN_CUOTAS";
            poliza.MotivoEstadoPago = "Pago por debito/cobro recurrente";
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            return;
        }
        if (ContainsAny(forma, "TRANSFERENCIA"))
        {
            poliza.EstadoPago = "EN_CUOTAS";
            poliza.MotivoEstadoPago = "Pago por transferencia/cuotas";
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            return;
        }

        poliza.EstadoPago = "SIN_VALIDAR";
        poliza.MotivoEstadoPago = null;
        poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
        MarkRevision(poliza, "Forma de pago no reconocida");
    }

    private static void ApplyTipoProceso(Poliza poliza, HistoricalRulesContext context)
    {
        if (!CanUpdate(poliza.OrigenTipoProceso, context.AllowOverwriteAutomaticOnly))
            return;

        var value = NormalizeRaw(context.EmisionRenovacionRaw);
        if (ContainsAny(value, "EMISION"))
        {
            poliza.TipoProceso = "EMISION";
            poliza.OrigenTipoProceso = "REGLA_AUTOMATICA";
        }
        else if (ContainsAny(value, "RENOVACION"))
        {
            poliza.TipoProceso = "RENOVACION";
            poliza.OrigenTipoProceso = "REGLA_AUTOMATICA";
        }
    }

    private static void ApplyEstadoPoliza(Poliza poliza, HistoricalRulesContext context)
    {
        if (!CanUpdate(poliza.OrigenEstadoPolizaReal, context.AllowOverwriteAutomaticOnly))
            return;

        var combined = NormalizeRaw($"{context.Observacion2} {context.Observaciones}");
        if (string.IsNullOrWhiteSpace(combined))
        {
            if (string.IsNullOrWhiteSpace(poliza.EstadoPolizaReal) || poliza.EstadoPolizaReal == "SIN_VALIDAR")
            {
                poliza.EstadoPolizaReal = "ACTIVA";
                poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            }
            return;
        }

        if (ContainsAny(combined, "NUNCA PAGO"))
        {
            poliza.EstadoPago = "MORA";
            poliza.MotivoEstadoPago = "Nunca pago";
            poliza.OrigenEstadoPago = "REGLA_AUTOMATICA";
            poliza.EstadoPolizaReal = "CANCELADA";
            poliza.MotivoCancelacion = "Nunca pago";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "EN PROCESO DE CANCELACION", "POLIZA SERA CANCELADA"))
        {
            poliza.EstadoPolizaReal = "PENDIENTE_CANCELACION";
            poliza.MotivoCancelacion = "Cancelacion en proceso";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "CANCELADA", "CANCELADO", "SOLICITO LA CANCELACION", "FALTA DE PAGO", "PERDIDA TOTAL"))
        {
            poliza.EstadoPolizaReal = "CANCELADA";
            poliza.MotivoCancelacion = ContainsAny(combined, "PERDIDA TOTAL") ? "Perdida total" : "Cancelada por observacion";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "NO RENOVO", "NO RENOVARA", "DE MOMENTO NO RENOVARA"))
        {
            poliza.EstadoPolizaReal = "NO_RENOVADA";
            poliza.MotivoCancelacion = "Cliente no renovo/no renovara";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "NO CONTESTA", "NO RESPONDE", "NO CONTESTA CORREOS", "NO CONTESTA WHATSAPP"))
        {
            poliza.EstadoPolizaReal = "PENDIENTE_CONTACTO";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            MarkRevision(poliza, "Cliente no responde");
            return;
        }

        if (ContainsAny(combined, "VENDIO EL VEHICULO", "YA NO TIENE EL VEHICULO", "CAMBIO DE VEHICULO"))
        {
            poliza.EstadoPolizaReal = "CANCELADA";
            poliza.MotivoCancelacion = "Vehiculo vendido/cambio de vehiculo";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "PASO A ATLANTIDA", "PASO A BANPAIS", "SE PASO A CREFISA", "CAMBIO DE COMPANIA", "CAMBIO DE AGENTE", "TOMO EL SEGURO CON"))
        {
            poliza.EstadoPolizaReal = "NO_RENOVADA";
            poliza.MotivoCancelacion = "Cambio de compania/agente";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (ContainsAny(combined, "SE FUE DEL PAIS"))
        {
            poliza.EstadoPolizaReal = "NO_RENOVADA";
            poliza.MotivoCancelacion = "Cliente fuera del pais";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
            return;
        }

        if (string.IsNullOrWhiteSpace(poliza.EstadoPolizaReal) || poliza.EstadoPolizaReal == "SIN_VALIDAR")
        {
            poliza.EstadoPolizaReal = "ACTIVA";
            poliza.OrigenEstadoPolizaReal = "REGLA_AUTOMATICA";
        }
    }

    private static void ApplySumaAsegurada(Poliza poliza, HistoricalRulesContext context)
    {
        if (!CanUpdate(poliza.OrigenSumaAsegurada, context.AllowOverwriteAutomaticOnly))
            return;

        var raw = EmptyToNull(context.SumaAseguradaOriginal) ?? EmptyToNull(context.SumaAseguradaLimpia);
        poliza.SumaAseguradaTextoOriginal = raw;
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var ramo = (poliza.RamoNormalizado ?? "").Trim().ToUpperInvariant();
        var medica = ramo is "MEDICO" or "VIDA_MEDICO";
        if (!medica)
        {
            var one = ParseDecimal(raw);
            if (one.HasValue)
                poliza.SumaAsegurada = one.Value;
            poliza.OrigenSumaAsegurada = "IMPORTACION_EXCEL";
            return;
        }

        var parts = raw.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseDecimal).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (parts.Count == 0)
        {
            poliza.OrigenSumaAsegurada = "IMPORTACION_EXCEL";
            MarkRevision(poliza, "Formato suma asegurada invalido");
            return;
        }
        if (parts.Count == 1)
        {
            poliza.SumaAseguradaVida = parts[0];
            poliza.SumaAsegurada = parts[0];
            poliza.MaximoVitalicio = null;
            poliza.OrigenSumaAsegurada = "REGLA_AUTOMATICA";
            return;
        }

        poliza.MaximoVitalicio = parts.Max();
        poliza.SumaAseguradaVida = parts.Min();
        poliza.SumaAsegurada = poliza.SumaAseguradaVida;
        poliza.OrigenSumaAsegurada = "REGLA_AUTOMATICA";
    }

    private static void ApplyReferido(Cliente? cliente, string? observaciones, string? observacion2)
    {
        if (cliente is null)
            return;
        var obs = EmptyToNull(observaciones);
        if (string.IsNullOrWhiteSpace(obs))
            return;
        var norm = NormalizeRaw($"{obs} {observacion2}");
        if (ContainsAny(norm, "CAMBIO TITULAR", "CANCELADA", "NO RENOVO", "FALTA DE PAGO", "NO RESPONDE", "NO CONTESTA"))
            return;
        if (!LooksLikeSimpleName(obs))
            return;

        cliente.ReferidoDetectado = true;
        cliente.ReferidoPorNombre = obs.Trim();
    }

    private static void ApplyEstadoRevision(Poliza poliza)
    {
        if (poliza.RequiereRevisionManual)
        {
            poliza.EstadoRevision = "PENDIENTE_REVISION";
            return;
        }

        if (string.IsNullOrWhiteSpace(poliza.EstadoRevision))
            poliza.EstadoRevision = "OK";
    }

    private static bool CanUpdate(string? origen, bool onlyAutomatic)
    {
        if (!onlyAutomatic)
            return !string.Equals(origen, "MANUAL", StringComparison.OrdinalIgnoreCase);

        return string.IsNullOrWhiteSpace(origen)
               || string.Equals(origen, "IMPORTACION_EXCEL", StringComparison.OrdinalIgnoreCase)
               || string.Equals(origen, "REGLA_AUTOMATICA", StringComparison.OrdinalIgnoreCase);
    }

    private static void MarkRevision(Poliza poliza, string reason)
    {
        poliza.RequiereRevisionManual = true;
        poliza.EstadoRevision = "PENDIENTE_REVISION";
        if (string.IsNullOrWhiteSpace(reason))
            return;
        if (string.IsNullOrWhiteSpace(poliza.MotivoRevision))
            poliza.MotivoRevision = reason;
        else if (!poliza.MotivoRevision.Contains(reason, StringComparison.OrdinalIgnoreCase))
            poliza.MotivoRevision = $"{poliza.MotivoRevision}; {reason}";
    }

    private static bool LooksLikeSimpleName(string value)
    {
        if (value.Contains("/"))
            return false;
        if (Regex.IsMatch(value, @"[\d@#{}[\]<>:;]"))
            return false;
        var words = Regex.Replace(value.Trim(), @"\s+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 1 or > 3)
            return false;
        return words.All(x => Regex.IsMatch(x, "^[A-Za-zÁÉÍÓÚÑáéíóúñ]+$"));
    }

    private static string NormalizeRaw(string? value)
    {
        var clean = RemoveDiacritics((value ?? "").Trim().ToUpperInvariant());
        clean = Regex.Replace(clean, @"[\t\r\n]+", " ");
        clean = Regex.Replace(clean, @"[^\w\s/\-]", " ");
        return Regex.Replace(clean, @"\s{2,}", " ").Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(NormalizationForm.FormC);
    }

    private static decimal? ParseDecimal(string value)
    {
        var clean = Regex.Replace(value ?? "", @"[^\d\.,\-]", "").Replace(",", "");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(n => value.Contains(n, StringComparison.Ordinal));

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class HistoricalRulesContext
{
    public string? RamoRaw { get; set; }
    public string? FormaPagoRaw { get; set; }
    public string? EmisionRenovacionRaw { get; set; }
    public string? Observacion2 { get; set; }
    public string? Observaciones { get; set; }
    public string? SumaAseguradaOriginal { get; set; }
    public string? SumaAseguradaLimpia { get; set; }
    public bool AllowOverwriteAutomaticOnly { get; set; }
    public Cliente? Cliente { get; set; }
}

public sealed record HistoricalRuleValidationResult(string CaseName, bool Passed, string Details);
