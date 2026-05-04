using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class DocumentoRepository
{
    private readonly DbConnectionFactory _factory;

    public DocumentoRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> InsertAsync(Documento documento)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO documentos
            (entidad_tipo, entidad_id, tipo_documento, nombre_archivo_original, nombre_archivo_guardado, ruta_relativa, ruta_archivo, mime_type, tamano_bytes, hash_archivo, subido_por_usuario_id, extension, activo, nombre_archivo, ruta, usuario_id, `tamaño`)
            VALUES
            (@EntidadTipo, @EntidadId, @TipoDocumento, @NombreArchivoOriginal, @NombreArchivoGuardado, @RutaRelativa, @RutaRelativa, @MimeType, @TamanoBytes, @HashArchivo, @SubidoPorUsuarioId, @Extension, @Activo, @NombreArchivoOriginal, @RutaRelativa, @SubidoPorUsuarioId, @TamanoBytes);
            SELECT LAST_INSERT_ID();";

        return await cn.ExecuteScalarAsync<int>(sql, documento);
    }

    public async Task<IEnumerable<DocumentoDto>> GetByEntidadAsync(string entidadTipo, int entidadId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT
                d.id Id,
                d.entidad_tipo EntidadTipo,
                d.entidad_id EntidadId,
                COALESCE(NULLIF(d.nombre_archivo_original,''), d.nombre_archivo) NombreArchivoOriginal,
                COALESCE(NULLIF(d.nombre_archivo_guardado,''), SUBSTRING_INDEX(COALESCE(NULLIF(d.ruta_relativa,''), COALESCE(NULLIF(d.ruta_archivo,''), d.ruta)), '/', -1)) NombreArchivoGuardado,
                COALESCE(NULLIF(d.ruta_relativa,''), COALESCE(NULLIF(d.ruta_archivo,''), d.ruta)) RutaRelativa,
                d.tipo_documento TipoDocumento,
                d.fecha_subida FechaSubida,
                d.subido_por_usuario_id SubidoPorUsuarioId,
                COALESCE(u.Username, 'Sistema') Usuario,
                COALESCE(NULLIF(d.tamano_bytes,0), d.`tamaño`) TamanoBytes,
                d.mime_type MimeType,
                d.extension Extension,
                d.hash_archivo HashArchivo,
                d.activo Activo
            FROM documentos d
            LEFT JOIN Users u ON u.Id = d.subido_por_usuario_id
            WHERE d.entidad_tipo = @entidadTipo
              AND d.entidad_id = @entidadId
              AND d.activo = 1
            ORDER BY d.fecha_subida DESC;";

        return await cn.QueryAsync<DocumentoDto>(sql, new { entidadTipo, entidadId });
    }

    public async Task<Documento?> GetByIdAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT
                id Id,
                entidad_tipo EntidadTipo,
                entidad_id EntidadId,
                COALESCE(NULLIF(nombre_archivo_original,''), nombre_archivo) NombreArchivoOriginal,
                COALESCE(NULLIF(nombre_archivo_guardado,''), SUBSTRING_INDEX(COALESCE(NULLIF(ruta_relativa,''), COALESCE(NULLIF(ruta_archivo,''), ruta)), '/', -1)) NombreArchivoGuardado,
                COALESCE(NULLIF(ruta_relativa,''), COALESCE(NULLIF(ruta_archivo,''), ruta)) RutaRelativa,
                tipo_documento TipoDocumento,
                fecha_subida FechaSubida,
                subido_por_usuario_id SubidoPorUsuarioId,
                COALESCE(NULLIF(tamano_bytes,0), `tamaño`) TamanoBytes,
                mime_type MimeType,
                extension Extension,
                hash_archivo HashArchivo,
                activo Activo
            FROM documentos
            WHERE id = @id;";

        return await cn.QueryFirstOrDefaultAsync<Documento>(sql, new { id });
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE documentos SET activo = 0 WHERE id = @id;", new { id });
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS documentos (
                id INT AUTO_INCREMENT PRIMARY KEY,
                entidad_tipo VARCHAR(30) NOT NULL,
                entidad_id INT NOT NULL,
                tipo_documento VARCHAR(80) NOT NULL,
                nombre_archivo_original VARCHAR(255) NOT NULL,
                ruta_archivo VARCHAR(500) NOT NULL,
                mime_type VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
                tamano_bytes BIGINT NOT NULL DEFAULT 0,
                subido_por_usuario_id INT NULL,
                fecha_subida DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                extension VARCHAR(10) NOT NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                INDEX ix_documentos_entidad (entidad_tipo, entidad_id),
                INDEX ix_documentos_fecha (fecha_subida)
            );
            ALTER TABLE documentos
                ADD COLUMN IF NOT EXISTS tipo_documento VARCHAR(80) NOT NULL DEFAULT 'OTRO',
                ADD COLUMN IF NOT EXISTS nombre_archivo_original VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS nombre_archivo_guardado VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS ruta_relativa VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS ruta_archivo VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS mime_type VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
                ADD COLUMN IF NOT EXISTS tamano_bytes BIGINT NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS hash_archivo VARCHAR(128) NULL,
                ADD COLUMN IF NOT EXISTS subido_por_usuario_id INT NULL,
                ADD COLUMN IF NOT EXISTS nombre_archivo VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS ruta VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS usuario_id INT NULL,
                ADD COLUMN IF NOT EXISTS `tamaño` BIGINT NULL,
                ADD COLUMN IF NOT EXISTS activo TINYINT(1) NOT NULL DEFAULT 1;");
    }
}
