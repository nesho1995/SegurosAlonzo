using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ReclamosWhatsApp.Services.DataQuality;

public sealed class PhoneNormalizationService
{
    private static readonly Regex Splitter = new(@"[,;/|\r\n]+|\s+(?:y|o)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExtensionMarker = new(@"\b(?:ext|extension|oficina|casa|cel|movil|hermano|hijo|esposa|madre|padre)\b.*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PhoneNormalizationResult NormalizeMany(string? raw)
    {
        var result = new PhoneNormalizationResult { Original = raw ?? "" };
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var candidates = Splitter.Split(raw)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .DefaultIfEmpty(raw.Trim())
            .ToList();

        foreach (var candidate in candidates)
        {
            var phone = NormalizeOne(candidate);
            if (phone is not null)
            {
                if (!result.Valid.Any(x => x.E164 == phone.E164))
                    result.Valid.Add(phone);
                continue;
            }

            var note = ExtractNote(candidate);
            if (!string.IsNullOrWhiteSpace(note))
                result.Notes.Add(note);

            result.Invalid.Add(candidate);
        }

        return result;
    }

    public string ToWhatsAppCloudNumber(string? e164)
    {
        return string.IsNullOrWhiteSpace(e164)
            ? ""
            : e164.Trim().TrimStart('+');
    }

    public string ToWhatsAppCloudFormat(string? e164) => ToWhatsAppCloudNumber(e164);

    private static NormalizedPhone? NormalizeOne(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var text = candidate.Trim();
        var hasExplicitCountry = text.StartsWith("+", StringComparison.Ordinal) || Regex.IsMatch(text, @"^\s*00\d+");
        text = ExtensionMarker.Replace(text, "");

        if (text.StartsWith("00", StringComparison.Ordinal))
            text = "+" + text[2..];

        var hasPlus = text.StartsWith("+", StringComparison.Ordinal);
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (digits.Length == 8 && !hasExplicitCountry)
            return new NormalizedPhone("+504" + digits, "504" + digits, true, "HN");

        if (digits.Length == 11 && digits.StartsWith("504", StringComparison.Ordinal))
            return new NormalizedPhone("+" + digits, digits, true, "HN");

        if (digits.Length == 10 && !hasExplicitCountry)
            return new NormalizedPhone("+1" + digits, "1" + digits, true, "US");

        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
            return new NormalizedPhone("+" + digits, digits, true, "US");

        return null;
    }

    private static string ExtractNote(string candidate)
    {
        var letters = Regex.Replace(candidate, @"[\d\s\-\+\(\)\.;,/|]", "").Trim();
        return string.IsNullOrWhiteSpace(letters) ? "" : candidate.Trim();
    }
}

public sealed class PhoneNormalizationResult
{
    public string Original { get; set; } = "";
    public List<NormalizedPhone> Valid { get; set; } = [];
    public List<string> Invalid { get; set; } = [];
    public List<string> Notes { get; set; } = [];

    [JsonIgnore]
    public NormalizedPhone? PrincipalPhone => Valid.FirstOrDefault();

    [JsonIgnore]
    public NormalizedPhone? SecondaryPhone => Valid.Skip(1).FirstOrDefault();

    public string Principal => PrincipalPhone?.E164 ?? "";
    public string Secondary => SecondaryPhone?.E164 ?? "";
    public IReadOnlyCollection<string> Extras => Valid.Skip(2).Select(x => x.E164).ToList();
    public string PrincipalWhatsApp => PrincipalPhone?.WhatsAppCloud ?? "";
    public bool WhatsappReady => PrincipalPhone?.WhatsappReady ?? false;
}

public sealed record NormalizedPhone(string E164, string WhatsAppCloud, bool WhatsappReady, string Country);
