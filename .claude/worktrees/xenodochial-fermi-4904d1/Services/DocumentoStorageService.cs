using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using System.Security.Cryptography;

namespace ReclamosWhatsApp.Services;

public class DocumentoStorageService
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> EntidadesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLIENTE", "POLIZA", "CUOTA", "PAGO", "RECLAMO"
    };

    private static readonly HashSet<string> ExtensionesPermitidasDefault = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".txt", ".doc", ".docx", ".xls", ".xlsx"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly DocumentoRepository _documentos;
    private readonly AuditoriaService _auditoria;
    private readonly IConfiguration _configuration;

    public DocumentoStorageService(
        IWebHostEnvironment environment,
        DocumentoRepository documentos,
        AuditoriaService auditoria,
        IConfiguration configuration)
    {
        _environment = environment;
        _documentos = documentos;
        _auditoria = auditoria;
        _configuration = configuration;
    }

    public async Task<DocumentoDto> GuardarAsync(IFormFile archivo, string entidadTipo, int entidadId, string tipoDocumento, int? usuarioId)
    {
        entidadTipo = NormalizarEntidad(entidadTipo);
        Validar(archivo, entidadTipo, entidadId);

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var nombreOriginal = Path.GetFileName(archivo.FileName);
        var tipo = string.IsNullOrWhiteSpace(tipoDocumento) ? "OTRO" : tipoDocumento.Trim().ToUpperInvariant();
        var baseFolder = _configuration["Documentos:CarpetaBase"] ?? Path.Combine("Uploads", "Documentos");
        var prefix = RegexSafe(tipo.ToLowerInvariant());
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var token = Guid.NewGuid().ToString("N")[..8];
        var nombreGuardado = $"{prefix}_{stamp}_{token}{extension}";
        var relativeFolder = Path.Combine(baseFolder, entidadTipo.ToUpperInvariant(), entidadId.ToString());
        var absoluteFolder = Path.Combine(_environment.ContentRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absolutePath = Path.Combine(absoluteFolder, nombreGuardado);
        await using (var stream = new FileStream(absolutePath, FileMode.CreateNew))
        {
            await archivo.CopyToAsync(stream);
        }
        var hash = ComputeSha256(absolutePath);
        var rutaRelativa = Path.Combine(relativeFolder, nombreGuardado).Replace("\\", "/");

        var id = await _documentos.InsertAsync(new Documento
        {
            EntidadTipo = entidadTipo,
            EntidadId = entidadId,
            NombreArchivoOriginal = nombreOriginal,
            NombreArchivoGuardado = nombreGuardado,
            RutaRelativa = rutaRelativa,
            TipoDocumento = tipo,
            SubidoPorUsuarioId = usuarioId,
            TamanoBytes = archivo.Length,
            MimeType = string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType,
            HashArchivo = hash,
            Extension = extension.TrimStart('.')
        });

        await _auditoria.RegistrarAsync(
            "SUBIR_DOCUMENTO",
            entidadTipo,
            entidadId,
            $"Documento subido: {nombreOriginal}");

        var items = await _documentos.GetByEntidadAsync(entidadTipo, entidadId);
        return items.First(x => x.Id == id);
    }

    public async Task<(Documento documento, string absolutePath)> PrepararDescargaAsync(int id)
    {
        var documento = await _documentos.GetByIdAsync(id)
            ?? throw new InvalidOperationException("El documento no existe.");

        var safeRelative = (documento.RutaRelativa ?? "").Trim().Replace("\\", "/").TrimStart('/');
        if (safeRelative.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Ruta invalida.");
        var absolutePath = Path.Combine(_environment.ContentRootPath, safeRelative);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("El archivo no esta disponible.");

        return (documento, absolutePath);
    }

    public async Task EliminarAsync(int id)
    {
        var documento = await _documentos.GetByIdAsync(id)
            ?? throw new InvalidOperationException("El documento no existe.");

        var safeRelative = (documento.RutaRelativa ?? "").Trim().Replace("\\", "/").TrimStart('/');
        if (safeRelative.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Ruta invalida.");
        var absolutePath = Path.Combine(_environment.ContentRootPath, safeRelative);
        await _documentos.DeleteAsync(id);

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        await _auditoria.RegistrarAsync(
            "ELIMINAR_DOCUMENTO",
            documento.EntidadTipo,
            documento.EntidadId,
            $"Documento eliminado: {documento.NombreArchivoOriginal}");
    }

    private void Validar(IFormFile archivo, string entidadTipo, int entidadId)
    {
        if (!EntidadesPermitidas.Contains(entidadTipo))
            throw new InvalidOperationException("El tipo de entidad no es valido.");

        if (entidadId <= 0)
            throw new InvalidOperationException("Selecciona una entidad valida.");

        if (archivo is null || archivo.Length == 0)
            throw new InvalidOperationException("Selecciona un archivo.");

        var configuredMax = MaxFileBytesFromConfig();
        if (archivo.Length > configuredMax)
        {
            throw new InvalidOperationException($"El archivo supera el limite permitido de {configuredMax / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(archivo.FileName);
        if (!GetAllowedExtensions().Contains(extension))
            throw new InvalidOperationException("Solo se permiten archivos PDF, JPG, JPEG, PNG, WEBP, TXT, DOC, DOCX, XLS y XLSX.");
    }

    private static string NormalizarEntidad(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }

    private static string RegexSafe(string value)
    {
        var cleaned = new string((value ?? "archivo").Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "archivo" : cleaned;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private long MaxFileBytesFromConfig()
    {
        if (long.TryParse(_configuration["Documentos:TamanoMaximoBytes"], out var max) && max > 0)
            return max;
        return MaxFileBytes;
    }

    private HashSet<string> GetAllowedExtensions()
    {
        var raw = _configuration["Documentos:ExtensionesPermitidas"];
        if (string.IsNullOrWhiteSpace(raw))
            return ExtensionesPermitidasDefault;

        var parsed = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : $".{x.ToLowerInvariant()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return parsed.Count == 0 ? ExtensionesPermitidasDefault : parsed;
    }
}
