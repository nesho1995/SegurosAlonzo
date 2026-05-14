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

    private static readonly Dictionary<string, string[]> MimePermitidosPorExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"],
        [".txt"] = ["text/plain"],
        [".doc"] = ["application/msword", "application/octet-stream"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
        [".xls"] = ["application/vnd.ms-excel", "application/octet-stream"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"]
    };

    private static readonly Dictionary<string, string> CarpetasPorEntidad = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CLIENTE"] = "clientes",
        ["POLIZA"] = "polizas",
        ["CUOTA"] = "cuotas",
        ["PAGO"] = "pagos",
        ["RECLAMO"] = "reclamos"
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
        var nombreOriginal = CleanOriginalFileName(Path.GetFileName(archivo.FileName));
        var tipo = string.IsNullOrWhiteSpace(tipoDocumento) ? "OTRO" : tipoDocumento.Trim().ToUpperInvariant();
        await ReemplazarSiDocumentoUnicoAsync(entidadTipo, entidadId, tipo);
        var baseFolder = CleanRelativeRoot(_configuration["Documentos:CarpetaBase"] ?? "storage");
        var folderEntidad = CarpetasPorEntidad[entidadTipo];
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var token = Guid.NewGuid().ToString("N");
        var nombreLimpio = SafeFileName(Path.GetFileNameWithoutExtension(nombreOriginal));
        var nombreGuardado = $"{stamp}_{token}_{nombreLimpio}{extension}";
        var relativeFolder = Path.Combine(baseFolder, folderEntidad, entidadId.ToString());
        var absoluteFolder = Path.Combine(_environment.ContentRootPath, relativeFolder);
        EnsureInsideContentRoot(absoluteFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absolutePath = Path.Combine(absoluteFolder, nombreGuardado);
        EnsureInsideContentRoot(absolutePath);
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

    public async Task<DocumentoDto> GuardarDesdeStreamAsync(
        Stream archivo,
        string nombreArchivo,
        string? contentType,
        long? tamanoBytes,
        string entidadTipo,
        int entidadId,
        string tipoDocumento,
        int? usuarioId,
        string? observacion = null)
    {
        entidadTipo = NormalizarEntidad(entidadTipo);
        if (!EntidadesPermitidas.Contains(entidadTipo))
            throw new InvalidOperationException("El tipo de entidad no es valido.");
        if (entidadId <= 0)
            throw new InvalidOperationException("Selecciona una entidad valida.");
        if (archivo is null || !archivo.CanRead)
            throw new InvalidOperationException("El archivo no esta disponible.");

        var nombreOriginal = CleanOriginalFileName(nombreArchivo);
        var mimeType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        var extension = Path.GetExtension(nombreOriginal).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ExtensionFromMime(mimeType);
            nombreOriginal = $"{Path.GetFileNameWithoutExtension(nombreOriginal)}{extension}";
        }

        var configuredMax = MaxFileBytesFromConfig();
        if (tamanoBytes.HasValue && tamanoBytes.Value > configuredMax)
            throw new InvalidOperationException($"El archivo supera el limite permitido de {configuredMax / (1024 * 1024)} MB.");
        if (!GetAllowedExtensions().Contains(extension))
            throw new InvalidOperationException("Solo se permiten archivos PDF, JPG, JPEG, PNG, WEBP, TXT, DOC, DOCX, XLS y XLSX.");
        if (!MimeLooksValid(extension, mimeType))
            throw new InvalidOperationException("El tipo de archivo no coincide con la extension.");

        var tipo = string.IsNullOrWhiteSpace(tipoDocumento) ? "OTRO" : tipoDocumento.Trim().ToUpperInvariant();
        await ReemplazarSiDocumentoUnicoAsync(entidadTipo, entidadId, tipo);
        var baseFolder = CleanRelativeRoot(_configuration["Documentos:CarpetaBase"] ?? "storage");
        var folderEntidad = CarpetasPorEntidad[entidadTipo];
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var token = Guid.NewGuid().ToString("N");
        var nombreLimpio = SafeFileName(Path.GetFileNameWithoutExtension(nombreOriginal));
        var nombreGuardado = $"{stamp}_{token}_{nombreLimpio}{extension}";
        var relativeFolder = Path.Combine(baseFolder, folderEntidad, entidadId.ToString());
        var absoluteFolder = Path.Combine(_environment.ContentRootPath, relativeFolder);
        EnsureInsideContentRoot(absoluteFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absolutePath = Path.Combine(absoluteFolder, nombreGuardado);
        EnsureInsideContentRoot(absolutePath);
        var bytesWritten = await CopyToNewFileWithLimitAsync(archivo, absolutePath, configuredMax);
        if (bytesWritten == 0)
        {
            File.Delete(absolutePath);
            throw new InvalidOperationException("Selecciona un archivo.");
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
            Observacion = CleanObservacion(observacion),
            SubidoPorUsuarioId = usuarioId,
            TamanoBytes = bytesWritten,
            MimeType = mimeType,
            HashArchivo = hash,
            Extension = extension.TrimStart('.')
        });
        if (!string.IsNullOrWhiteSpace(observacion))
            await _documentos.UpdateObservacionAsync(id, observacion);

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
        EnsureInsideContentRoot(absolutePath);
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
        EnsureInsideContentRoot(absolutePath);
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

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!GetAllowedExtensions().Contains(extension))
            throw new InvalidOperationException("Solo se permiten archivos PDF, JPG, JPEG, PNG, WEBP, TXT, DOC, DOCX, XLS y XLSX.");

        if (!MimeLooksValid(extension, archivo.ContentType))
            throw new InvalidOperationException("El tipo de archivo no coincide con la extension.");
    }

    private static string NormalizarEntidad(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }

    private static string SafeFileName(string value)
    {
        var cleaned = new string((value ?? "archivo")
            .Normalize()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        cleaned = string.Join("_", cleaned.Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(cleaned))
            return "archivo";
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    private static string CleanOriginalFileName(string value)
    {
        var fileName = Path.GetFileName(value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return "archivo";

        var cleaned = new string(fileName
            .Where(ch => !char.IsControl(ch) && ch != '"' && ch != '\\' && ch != '/')
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "archivo" : cleaned;
    }

    private static string? CleanObservacion(string? value)
    {
        var cleaned = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;
        return cleaned.Length > 1000 ? cleaned[..1000] : cleaned;
    }

    private static string CleanRelativeRoot(string value)
    {
        var clean = (value ?? "storage").Trim().Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(clean) || clean.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(clean))
            return "storage";
        return clean;
    }

    private void EnsureInsideContentRoot(string path)
    {
        var root = Path.GetFullPath(_environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta fuera del almacenamiento permitido.");
    }

    private static bool MimeLooksValid(string extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return true;

        return MimePermitidosPorExtension.TryGetValue(extension, out var allowed)
               && allowed.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase);
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

    private async Task ReemplazarSiDocumentoUnicoAsync(string entidadTipo, int entidadId, string tipoDocumento)
    {
        if (!string.Equals(entidadTipo, "RECLAMO", StringComparison.OrdinalIgnoreCase))
            return;
        if (EsDocumentoMultiplePermitido(tipoDocumento))
            return;

        await _documentos.DeleteActiveByEntidadAndTipoAsync(entidadTipo, entidadId, tipoDocumento);
    }

    private static bool EsDocumentoMultiplePermitido(string? tipoDocumento)
    {
        return !string.IsNullOrWhiteSpace(tipoDocumento)
            && tipoDocumento.Contains("COTIZACION", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtensionFromMime(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "text/plain" => ".txt",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => ".bin"
        };
    }

    private static async Task<long> CopyToNewFileWithLimitAsync(Stream source, string absolutePath, long maxBytes)
    {
        var buffer = new byte[81920];
        long total = 0;
        await using var target = new FileStream(absolutePath, FileMode.CreateNew);
        while (true)
        {
            var read = await source.ReadAsync(buffer);
            if (read == 0)
                break;
            total += read;
            if (total > maxBytes)
            {
                target.Close();
                File.Delete(absolutePath);
                throw new InvalidOperationException($"El archivo supera el limite permitido de {maxBytes / (1024 * 1024)} MB.");
            }
            await target.WriteAsync(buffer.AsMemory(0, read));
        }
        return total;
    }
}
