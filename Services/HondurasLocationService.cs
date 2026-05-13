using System.Globalization;
using System.Text;

namespace ReclamosWhatsApp.Services;

public static class HondurasLocationService
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TGU"] = "TEGUCIGALPA",
        ["DISTRITO CENTRAL"] = "TEGUCIGALPA",
        ["SPS"] = "SAN PEDRO SULA",
        ["S.P.S"] = "SAN PEDRO SULA",
        ["SAN PEDRO"] = "SAN PEDRO SULA"
    };

    private static readonly string[] Cities =
    [
        "TEGUCIGALPA",
        "COMAYAGUELA",
        "SAN PEDRO SULA",
        "LA CEIBA",
        "CHOLUTECA",
        "COMAYAGUA",
        "EL PROGRESO",
        "CHOLOMA",
        "DANLI",
        "JUTICALPA",
        "SANTA ROSA DE COPAN",
        "SIGUATEPEQUE",
        "PUERTO CORTES",
        "TOCOA",
        "VILLANUEVA",
        "LA LIMA",
        "CATACAMAS",
        "OLANCHITO",
        "YORO",
        "NACAOME",
        "LA PAZ",
        "GRACIAS",
        "MARCALA",
        "TALANGA",
        "EL PARAISO",
        "SANTA BARBARA",
        "LA ENTRADA",
        "COPAN RUINAS",
        "SANTA RITA",
        "POTRERILLOS",
        "PUERTO LEMPIRA",
        "TRUJILLO",
        "ROATAN",
        "TELA",
        "PROGRESO",
        "SABA",
        "BONITO ORIENTAL",
        "SAN LORENZO",
        "VALLE DE ANGELES",
        "AMARATECA",
        "GUAIMACA",
        "ZAMBRANO",
        "DANLI",
        "TROJES",
        "QUIMISTAN",
        "MACUELIZO",
        "NARANJITO",
        "SAN MARCOS",
        "OCOTEPEQUE",
        "LA ESPERANZA",
        "INTIBUCA",
        "LEMPIRA",
        "YUSCARAN",
        "MOROCELI",
        "CAMPAMENTO",
        "GUALACO",
        "GUAYAPE",
        "JESUS DE OTORO",
        "PIMIENTA",
        "SAN MANUEL",
        "SANTA CRUZ DE YOJOA",
        "VILLA DE SAN ANTONIO",
        "TAULABE",
        "AJUTERIQUE",
        "LEJAMANI",
        "GOASCORAN",
        "LANGUE",
        "ARAMECINA",
        "COXEN HOLE",
        "FRENCH HARBOUR",
        "GUANAJA",
        "UTILA"
    ];

    public static string? DetectCity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = NormalizeForMatch(text);
        foreach (var alias in Aliases)
        {
            if (ContainsToken(normalized, NormalizeForMatch(alias.Key)))
                return alias.Value;
        }

        foreach (var city in Cities.OrderByDescending(x => x.Length))
        {
            if (ContainsToken(normalized, NormalizeForMatch(city)))
                return city.ToUpperInvariant();
        }

        return null;
    }

    public static bool IsTegucigalpa(string? city)
    {
        return string.Equals(DetectCity(city) ?? NormalizeForMatch(city), "TEGUCIGALPA", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSanPedroSula(string? city)
    {
        return string.Equals(DetectCity(city) ?? NormalizeForMatch(city), "SAN PEDRO SULA", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsToken(string text, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return text == value
            || text.StartsWith(value + " ", StringComparison.Ordinal)
            || text.EndsWith(" " + value, StringComparison.Ordinal)
            || text.Contains(" " + value + " ", StringComparison.Ordinal)
            || text.Contains("," + value + ",", StringComparison.Ordinal)
            || text.Contains(" " + value + ",", StringComparison.Ordinal)
            || text.Contains("," + value + " ", StringComparison.Ordinal);
    }

    private static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
