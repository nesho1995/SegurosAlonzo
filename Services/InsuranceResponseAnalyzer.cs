using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public sealed record InsuranceResponseAnalysis(
    bool Aprobado,
    bool Denegado,
    bool RequiereRsa,
    bool RequiereCoaseguro,
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
            "RECHAZADO",
            "RECHAZADA",
            "DECLINADO",
            "DECLINADA",
            "NO PROCEDE",
            "IMPROCEDENTE",
            "SIN COBERTURA");

        var approved = !denied && ContainsAny(text,
            "APROBADO",
            "APROBADA",
            "APROBACION",
            "ACEPTADO",
            "ACEPTADA",
            "PROCEDENTE",
            "AUTORIZADO",
            "AUTORIZADA",
            "AUTORIZAMOS",
            "SE AUTORIZA",
            "PUEDE INGRESAR",
            "INGRESAR EL VEHICULO",
            "INGRESAR EL CARRO",
            "INGRESAR LA UNIDAD",
            "INGRESO DEL VEHICULO",
            "INGRESO DEL CARRO",
            "INGRESO DE LA UNIDAD",
            "PROCEDER CON LA REPARACION",
            "PROCEDER CON REPARACION");

        var mentionsRsa = ContainsAny(text, "RSA", "RESTITUCION DE SUMA ASEGURADA", "RESTITUCION SUMA ASEGURADA");
        var mentionsCoaseguro = ContainsAny(text, "COASEGURO", "CO ASEGURO");
        var requiresRsa = mentionsRsa && !ContainsAny(text,
            "NO REQUIERE RSA",
            "NO APLICA RSA",
            "SIN RSA",
            "SIN PAGO DE RSA");
        var requiresCoaseguro = mentionsCoaseguro && !ContainsAny(text,
            "NO REQUIERE COASEGURO",
            "NO REQUIERE CO ASEGURO",
            "NO APLICA COASEGURO",
            "NO APLICA CO ASEGURO",
            "SIN COASEGURO",
            "SIN CO ASEGURO",
            "SIN PAGO DE COASEGURO",
            "SIN PAGO DE CO ASEGURO",
            "SIN PAGAR COASEGURO",
            "SIN PAGAR CO ASEGURO");

        var noFinalPayments = ContainsAny(text,
            "SIN PAGO DE RSA",
            "NO REQUIERE RSA",
            "NO APLICA RSA",
            "SIN RSA",
            "SIN PAGO DE COASEGURO",
            "NO REQUIERE COASEGURO",
            "NO REQUIERE CO ASEGURO",
            "NO APLICA COASEGURO",
            "NO APLICA CO ASEGURO",
            "SIN COASEGURO",
            "SIN CO ASEGURO",
            "SIN PAGAR COASEGURO",
            "SIN PAGAR CO ASEGURO",
            "PUEDE INGRESAR EL CARRO",
            "PUEDE INGRESAR EL VEHICULO",
            "PUEDE INGRESAR LA UNIDAD");

        var moreDocs = ContainsAny(text,
            "DOCUMENTO ADICIONAL",
            "DOCUMENTOS ADICIONALES",
            "DOCUMENTACION ADICIONAL",
            "DOCUMENTACION PENDIENTE",
            "FALTA DOCUMENTO",
            "FALTAN DOCUMENTOS",
            "HACE FALTA",
            "NECESITAMOS",
            "SE REQUIERE",
            "REQUERIMOS",
            "PENDIENTE DE RECIBIR",
            "AMPLIAR INFORMACION",
            "INFORMACION ADICIONAL",
            "ENVIAR NUEVAMENTE",
            "CORREGIR");

        var isApproved = approved || (!denied && (requiresRsa || requiresCoaseguro));
        var hasSignals = isApproved || denied || mentionsRsa || mentionsCoaseguro || noFinalPayments || moreDocs;

        return new InsuranceResponseAnalysis(
            isApproved,
            denied,
            requiresRsa,
            requiresCoaseguro,
            isApproved && !requiresRsa && !requiresCoaseguro && noFinalPayments,
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
}
