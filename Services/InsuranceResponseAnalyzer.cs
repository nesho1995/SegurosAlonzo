using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public sealed record InsuranceResponseAnalysis(
    bool Aprobado,
    bool Denegado,
    bool RequiereRsa,
    bool RequiereDeducible,
    decimal? MontoRsa,
    decimal? MontoDeducible,
    bool AprobadoSinPagosFinales,
    bool SolicitaMasDocumentos,
    bool TieneSenales);

public static class InsuranceResponseAnalyzer
{
    public static InsuranceResponseAnalysis Analyze(EmailMessageDto email)
        => Analyze($"{email.Subject}\n{email.Body}");

    public static InsuranceResponseAnalysis Analyze(string? value)
    {
        var text = NormalizeForMatch(value);
        var denied = ContainsAny(text,
            "NO APROBADO",
            "NO APROBADA",
            "NO HA SIDO APROBADO",
            "NO HA SIDO APROBADA",
            "NO SE APRUEBA",
            "NO AUTORIZADO",
            "NO AUTORIZADA",
            "NO SE AUTORIZA",
            "RECHAZADO",
            "RECHAZADA",
            "RECHAZAMOS",
            "SE RECHAZA",
            "DECLINADO",
            "DECLINADA",
            "NO PROCEDE",
            "NO PROCEDENTE",
            "NO ES PROCEDENTE",
            "NO PROCEDEMOS",
            "NO PROCEDE EL RECLAMO",
            "IMPROCEDENTE",
            "SIN COBERTURA",
            "NO CUBIERTO",
            "NO CUBIERTA",
            "NO TIENE COBERTURA",
            "FUERA DE COBERTURA",
            "EXCLUSION",
            "EXCLUIDO",
            "EXCLUIDA");

        var approved = !denied && ContainsAny(text,
            "APROBADO",
            "APROBADA",
            "APROBACION",
            "APROBAMOS",
            "SE APRUEBA",
            "QUEDA APROBADO",
            "QUEDA APROBADA",
            "CASO APROBADO",
            "RECLAMO APROBADO",
            "SINIESTRO APROBADO",
            "ACEPTADO",
            "ACEPTADA",
            "PROCEDENTE",
            "PROCEDE",
            "PROCEDEMOS",
            "PROCEDER",
            "AUTORIZADO",
            "AUTORIZADA",
            "AUTORIZACION",
            "AUTORIZAMOS",
            "SE AUTORIZA",
            "AUTORIZACION DE INGRESO",
            "AUTORIZACION PARA REPARACION",
            "AUTORIZA INGRESO",
            "AUTORIZAMOS INGRESO",
            "AUTORIZAMOS REPARACION",
            "ORDEN DE REPARACION",
            "PUEDE INGRESAR",
            "PUEDE DAR INGRESO",
            "DAR INGRESO",
            "INGRESAR EL VEHICULO",
            "INGRESAR EL CARRO",
            "INGRESAR LA UNIDAD",
            "INGRESAR A TALLER",
            "INGRESO A TALLER",
            "INGRESO DEL VEHICULO",
            "INGRESO DEL CARRO",
            "INGRESO DE LA UNIDAD",
            "PUEDE LLEVAR EL VEHICULO",
            "PUEDE LLEVAR LA UNIDAD",
            "PUEDE PRESENTAR EL VEHICULO",
            "PUEDE PRESENTAR LA UNIDAD",
            "PASE A TALLER",
            "TALLER AUTORIZADO",
            "PROCEDER CON LA REPARACION",
            "PROCEDER CON REPARACION");

        var mentionsRsa = ContainsAny(text,
            "RSA",
            "RESTITUCION",
            "RESTITUCION DE SUMA ASEGURADA",
            "RESTITUCION SUMA ASEGURADA",
            "RESTITUIR SUMA ASEGURADA",
            "REINTEGRO DE SUMA ASEGURADA",
            "REINTEGRAR SUMA ASEGURADA",
            "RECUPERACION DE SUMA ASEGURADA",
            "RESTABLECIMIENTO DE SUMA ASEGURADA",
            "REPOSICION DE SUMA ASEGURADA");
        var mentionsCoaseguro = ContainsAny(text,
            "COASEGURO",
            "CO ASEGURO",
            "CO-SEGURO",
            "CO-PAGO",
            "COPAGO",
            "CO PAGO",
            "COPARTICIPACION",
            "CO PARTICIPACION",
            "PARTICIPACION DEL ASEGURADO",
            "PARTICIPACION ASEGURADO",
            "APORTE DEL ASEGURADO",
            "APORTE ASEGURADO",
            "PORCENTAJE DEL ASEGURADO");
        var mentionsDeducible = ContainsAny(text,
            "DEDUCIBLE",
            "DEDUCIBLES",
            "PAGO DE DEDUCIBLE",
            "PAGAR DEDUCIBLE",
            "CUBRIR DEDUCIBLE",
            "CANCELAR DEDUCIBLE");
        var requiresRsa = mentionsRsa && !ContainsAny(text,
            "NO REQUIERE RSA",
            "NO APLICA RSA",
            "NO DEBE PAGAR RSA",
            "NO CORRESPONDE RSA",
            "NO SOLICITAR RSA",
            "NO SOLICITA RSA",
            "NO SE REQUIERE RSA",
            "NO ES NECESARIO RSA",
            "EXENTO DE RSA",
            "SIN RSA",
            "SIN PAGO DE RSA");
        var requiresDeducible = (mentionsDeducible || mentionsCoaseguro) && !ContainsAny(text,
            "SIN DEDUCIBLE",
            "NO REQUIERE DEDUCIBLE",
            "NO APLICA DEDUCIBLE",
            "NO DEBE PAGAR DEDUCIBLE",
            "NO CORRESPONDE DEDUCIBLE",
            "NO SE REQUIERE DEDUCIBLE",
            "EXENTO DE DEDUCIBLE",
            "SIN PAGO DE DEDUCIBLE",
            "NO HAY DEDUCIBLE",
            "NO REQUIERE COASEGURO",
            "NO REQUIERE CO ASEGURO",
            "NO REQUIERE CO-SEGURO",
            "NO APLICA COASEGURO",
            "NO APLICA CO ASEGURO",
            "NO APLICA CO-SEGURO",
            "NO DEBE PAGAR COASEGURO",
            "NO DEBE PAGAR CO ASEGURO",
            "NO DEBE PAGAR CO-SEGURO",
            "NO CORRESPONDE COASEGURO",
            "NO SOLICITAR COASEGURO",
            "NO SOLICITA COASEGURO",
            "NO SE REQUIERE COASEGURO",
            "NO ES NECESARIO COASEGURO",
            "EXENTO DE COASEGURO",
            "EXENTO DE CO ASEGURO",
            "EXENTO DE CO-SEGURO",
            "SIN COASEGURO",
            "SIN CO ASEGURO",
            "SIN CO-SEGURO",
            "SIN PAGO DE COASEGURO",
            "SIN PAGO DE CO ASEGURO",
            "SIN PAGO DE CO-SEGURO",
            "SIN PAGAR COASEGURO",
            "SIN PAGAR CO ASEGURO",
            "SIN PAGAR CO-SEGURO",
            "SIN CO-PAGO",
            "SIN COPAGO",
            "NO APLICA COPAGO",
            "NO REQUIERE COPAGO");

        var noFinalPayments = ContainsAny(text,
            "SIN PAGOS FINALES",
            "NO HAY PAGOS PENDIENTES",
            "NO HAY PAGO PENDIENTE",
            "NO DEBE REALIZAR PAGO",
            "NO DEBE REALIZAR PAGOS",
            "NO DEBE PAGAR",
            "INGRESO SIN PAGO",
            "PUEDE INGRESAR SIN PAGO",
            "PUEDE INGRESAR SIN PAGOS",
            "SIN PAGO DE RSA",
            "NO REQUIERE RSA",
            "NO APLICA RSA",
            "NO DEBE PAGAR RSA",
            "NO CORRESPONDE RSA",
            "NO SE REQUIERE RSA",
            "NO ES NECESARIO RSA",
            "EXENTO DE RSA",
            "SIN RSA",
            "SIN PAGO DE COASEGURO",
            "NO REQUIERE COASEGURO",
            "NO REQUIERE CO ASEGURO",
            "NO REQUIERE CO-SEGURO",
            "NO APLICA COASEGURO",
            "NO APLICA CO ASEGURO",
            "NO APLICA CO-SEGURO",
            "NO DEBE PAGAR COASEGURO",
            "NO DEBE PAGAR CO ASEGURO",
            "NO DEBE PAGAR CO-SEGURO",
            "NO CORRESPONDE COASEGURO",
            "NO SE REQUIERE COASEGURO",
            "NO ES NECESARIO COASEGURO",
            "EXENTO DE COASEGURO",
            "EXENTO DE CO ASEGURO",
            "EXENTO DE CO-SEGURO",
            "SIN COASEGURO",
            "SIN CO ASEGURO",
            "SIN CO-SEGURO",
            "SIN PAGAR COASEGURO",
            "SIN PAGAR CO ASEGURO",
            "SIN PAGAR CO-SEGURO",
            "SIN CO-PAGO",
            "SIN COPAGO",
            "NO APLICA COPAGO",
            "NO REQUIERE COPAGO",
            "PUEDE INGRESAR EL CARRO",
            "PUEDE INGRESAR EL VEHICULO",
            "PUEDE INGRESAR LA UNIDAD");

        var moreDocs = ContainsAny(text,
            "DOCUMENTO ADICIONAL",
            "DOCUMENTOS ADICIONALES",
            "DOCUMENTACION ADICIONAL",
            "DOCUMENTACION PENDIENTE",
            "PENDIENTE DOCUMENTACION",
            "PENDIENTE INFORMACION",
            "FALTA DOCUMENTO",
            "FALTAN DOCUMENTOS",
            "FALTA INFORMACION",
            "FALTAN DATOS",
            "FALTA DATO",
            "HACE FALTA",
            "NECESITAMOS",
            "SOLICITAMOS",
            "SE SOLICITA",
            "SE REQUIERE",
            "REQUERIMOS",
            "PENDIENTE DE RECIBIR",
            "AMPLIAR INFORMACION",
            "INFORMACION ADICIONAL",
            "COMPLETAR DOCUMENTACION",
            "COMPLETAR INFORMACION",
            "REMITIR",
            "REMITIR DOCUMENTO",
            "ENVIAR DOCUMENTO",
            "ENVIAR NUEVAMENTE",
            "ADJUNTAR",
            "ADJUNTAR DOCUMENTO",
            "ACLARAR",
            "ACLARACION",
            "ACLARACIONES",
            "SUBSANAR",
            "AGRADECEMOS ENVIAR",
            "CORREGIR");

        var isApproved = approved || (!denied && (requiresRsa || requiresDeducible));
        var hasSignals = isApproved || denied || mentionsRsa || mentionsCoaseguro || mentionsDeducible || noFinalPayments || moreDocs;
        var montoRsa = requiresRsa ? DetectAmountNear(value, "RSA", "RESTITUCION", "RESTITUIR", "SUMA ASEGURADA") : null;
        var montoDeducible = requiresDeducible ? DetectAmountNear(value, "DEDUCIBLE", "DEDUCIBLES", "COASEGURO", "CO ASEGURO", "COPAGO", "CO PAGO") : null;

        return new InsuranceResponseAnalysis(
            isApproved,
            denied,
            requiresRsa,
            requiresDeducible,
            montoRsa,
            montoDeducible,
            isApproved && !requiresRsa && !requiresDeducible && noFinalPayments,
            moreDocs,
            hasSignals);
    }

    public static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private static decimal? DetectAmountNear(string? raw, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = NormalizeForMatch(raw);
        foreach (var term in terms.Select(NormalizeForMatch).Where(x => x.Length > 0))
        {
            foreach (Match match in Regex.Matches(text, Regex.Escape(term), RegexOptions.CultureInvariant))
            {
                var start = Math.Max(0, match.Index - 80);
                var length = Math.Min(text.Length - start, match.Length + 160);
                var window = text.Substring(start, length);
                var amount = ExtractFirstAmount(window);
                if (amount.HasValue)
                    return amount;
            }
        }

        return null;
    }

    private static decimal? ExtractFirstAmount(string text)
    {
        var match = Regex.Match(
            text,
            @"(?:\b(?:L|LPS|HNL)\.?\s*)?([0-9]{1,3}(?:[,.][0-9]{3})+(?:[,.][0-9]{2})?|[0-9]+(?:[,.][0-9]{2})?)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        var value = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');
        var decimalSeparator = lastComma > lastDot ? ',' : '.';
        var hasDecimal = (decimalSeparator == ',' && lastComma >= 0 && value.Length - lastComma - 1 == 2)
            || (decimalSeparator == '.' && lastDot >= 0 && value.Length - lastDot - 1 == 2);

        if (hasDecimal)
        {
            var normalized = value
                .Replace(decimalSeparator == ',' ? "." : ",", "")
                .Replace(decimalSeparator, '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;
        }

        var whole = value.Replace(",", "").Replace(".", "");
        return decimal.TryParse(whole, NumberStyles.Number, CultureInfo.InvariantCulture, out var wholeAmount) ? wholeAmount : null;
    }
}
