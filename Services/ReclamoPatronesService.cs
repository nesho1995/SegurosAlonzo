using System.Text.RegularExpressions;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ReclamoPatronesService
{
    private readonly ReclamoPatronesRepository _repo;

    public ReclamoPatronesService(ReclamoPatronesRepository repo)
    {
        _repo = repo;
    }

    public async Task<ProbarPatronesResult> ProbarExtraccionAsync(ProbarPatronesRequest request)
    {
        var plantillas = (await _repo.GetPlantillasAsync()).Where(x => x.Activa).OrderBy(x => x.Prioridad).ToList();
        var patrones = (await _repo.GetPatronesAsync()).Where(x => x.Activo).OrderBy(x => x.Prioridad).ToList();
        if (request.PlantillaId.HasValue)
            plantillas = plantillas.Where(x => x.Id == request.PlantillaId.Value).ToList();

        foreach (var plantilla in plantillas)
        {
            var patronIds = (await _repo.GetPlantillaReglasAsync(plantilla.Id)).Select(x => x.PatronId).ToHashSet();
            var scopedPatrones = patrones.Where(x => patronIds.Contains(x.Id)).ToList();
            var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reglaPorCampo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var patron in scopedPatrones)
            {
                if (TryApplyRule(patron, request.Subject, request.Body, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    if (!campos.ContainsKey(patron.CampoDestino))
                    {
                        campos[patron.CampoDestino] = value;
                        reglaPorCampo[patron.CampoDestino] = patron.Nombre;
                    }
                }
            }

            var faltantes = scopedPatrones
                .Where(x => x.Requerido)
                .Select(x => x.CampoDestino)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(c => !campos.ContainsKey(c))
                .ToList();

            var condiciones = (await _repo.GetCondicionesAsync(plantilla.Id)).ToList();
            var condicionesCumplen = EvaluateCondiciones(condiciones, request.Subject, request.Body);
            var cumple = faltantes.Count == 0 && condicionesCumplen;
            if (cumple || request.PlantillaId.HasValue)
            {
                return new ProbarPatronesResult
                {
                    PlantillaId = plantilla.Id,
                    PlantillaNombre = plantilla.Nombre,
                    PlantillaCumple = cumple,
                    CamposDetectados = campos,
                    CamposFaltantes = faltantes,
                    ReglaQueDetectoPorCampo = reglaPorCampo
                };
            }
        }

        return new ProbarPatronesResult
        {
            PlantillaCumple = false,
            PlantillaNombre = "Sin plantilla coincidente"
        };
    }

    private static bool EvaluateCondiciones(List<CorreoReclamoCondicion> condiciones, string subject, string body)
    {
        if (condiciones.Count == 0)
            return true;

        var grouped = condiciones.GroupBy(x => x.GrupoCondicion);
        foreach (var group in grouped)
        {
            var list = group.ToList();
            var isOr = list.Any(x => string.Equals(x.OperadorGrupo, "OR", StringComparison.OrdinalIgnoreCase));
            var results = list.Select(cond => EvalCondition(cond, subject, body)).ToList();
            if (isOr)
            {
                if (!results.Any(x => x))
                    return false;
            }
            else
            {
                if (results.Any(x => !x))
                    return false;
            }
        }

        return true;
    }

    private static bool EvalCondition(CorreoReclamoCondicion cond, string subject, string body)
    {
        var source = SelectSource(cond.Fuente, subject, body);
        var t = (cond.TipoRegla ?? "CONTIENE").Trim().ToUpperInvariant();
        return t switch
        {
            "REGEX" => Regex.IsMatch(source, cond.Patron ?? "", RegexOptions.IgnoreCase),
            "EMPIEZA_CON" => source.StartsWith(cond.Patron ?? "", StringComparison.OrdinalIgnoreCase),
            "TERMINA_CON" => source.EndsWith(cond.Patron ?? "", StringComparison.OrdinalIgnoreCase),
            _ => source.Contains(cond.Patron ?? "", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool TryApplyRule(CorreoReclamoPatron patron, string subject, string body, out string value)
    {
        value = "";
        var source = SelectSource(patron.Fuente, subject, body);
        var tipo = (patron.TipoRegla ?? "REGEX").Trim().ToUpperInvariant();

        if (tipo == "REGEX")
        {
            var match = Regex.Match(source, patron.Patron ?? "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return false;

            if (!string.IsNullOrWhiteSpace(patron.GrupoRegex))
            {
                value = match.Groups[patron.GrupoRegex]?.Value ?? "";
            }
            else if (match.Groups.Count > 1)
            {
                value = match.Groups[1].Value;
            }
            else
            {
                value = match.Value;
            }
        }
        else if (tipo == "CONTIENE")
        {
            value = source.Contains(patron.Patron ?? "", StringComparison.OrdinalIgnoreCase) ? (patron.Patron ?? "") : "";
        }
        else if (tipo == "EMPIEZA_CON")
        {
            value = source.StartsWith(patron.Patron ?? "", StringComparison.OrdinalIgnoreCase) ? (patron.Patron ?? "") : "";
        }
        else if (tipo == "TERMINA_CON")
        {
            value = source.EndsWith(patron.Patron ?? "", StringComparison.OrdinalIgnoreCase) ? (patron.Patron ?? "") : "";
        }

        value = (value ?? "").Trim();
        if (patron.NormalizarTexto)
            value = Regex.Replace(value, @"\s+", " ").Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string SelectSource(string? fuente, string subject, string body)
    {
        return (fuente ?? "SUBJECT_BODY").Trim().ToUpperInvariant() switch
        {
            "SUBJECT" => subject ?? "",
            "BODY" => body ?? "",
            _ => $"{subject}\n{body}"
        };
    }
}
