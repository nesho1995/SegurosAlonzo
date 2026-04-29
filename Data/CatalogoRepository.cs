using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class CatalogoRepository
{
    private readonly DbConnectionFactory _factory;

    public CatalogoRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS catalogos (
                id INT AUTO_INCREMENT PRIMARY KEY,
                tipo_catalogo VARCHAR(60) NOT NULL,
                codigo VARCHAR(120) NOT NULL,
                nombre VARCHAR(180) NOT NULL,
                descripcion TEXT NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                orden INT NOT NULL DEFAULT 0,
                es_default TINYINT(1) NOT NULL DEFAULT 0,
                pendiente_revision TINYINT(1) NOT NULL DEFAULT 0,
                fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                fecha_actualizacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uq_catalogos_tipo_codigo (tipo_catalogo, codigo),
                INDEX ix_catalogos_tipo_activo_orden (tipo_catalogo, activo, orden)
            );");
    }

    public async Task<IEnumerable<CatalogoItem>> GetByTipoAsync(string tipo, bool incluirInactivos = true)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<CatalogoItem>(@"
            SELECT
                id Id,
                tipo_catalogo TipoCatalogo,
                codigo Codigo,
                nombre Nombre,
                descripcion Descripcion,
                activo Activo,
                orden Orden,
                es_default EsDefault,
                pendiente_revision PendienteRevision,
                fecha_creacion FechaCreacion,
                fecha_actualizacion FechaActualizacion
            FROM catalogos
            WHERE tipo_catalogo = @tipo
              AND (@incluirInactivos = 1 OR activo = 1)
            ORDER BY activo DESC, orden ASC, nombre ASC;", new { tipo, incluirInactivos });
    }

    public async Task<IEnumerable<string>> GetTiposAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<string>(@"
            SELECT DISTINCT tipo_catalogo
            FROM catalogos
            ORDER BY tipo_catalogo;");
    }

    public async Task<int> UpsertAsync(CatalogoItem item)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        if (item.Id > 0)
        {
            await cn.ExecuteAsync(@"
                UPDATE catalogos
                SET nombre = @Nombre,
                    codigo = @Codigo,
                    descripcion = @Descripcion,
                    activo = @Activo,
                    orden = @Orden,
                    es_default = @EsDefault,
                    pendiente_revision = @PendienteRevision
                WHERE id = @Id;", item);
            return item.Id;
        }

        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO catalogos
            (tipo_catalogo, codigo, nombre, descripcion, activo, orden, es_default, pendiente_revision)
            VALUES
            (@TipoCatalogo, @Codigo, @Nombre, @Descripcion, @Activo, @Orden, @EsDefault, @PendienteRevision);
            SELECT LAST_INSERT_ID();", item);
    }

    public async Task SetActivoAsync(int id, bool activo)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE catalogos SET activo = @activo WHERE id = @id;", new { id, activo });
    }

    public async Task<int> EnsureValueAsync(string tipo, string rawValue, bool pendienteRevision)
    {
        await EnsureSchemaAsync();
        if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(rawValue))
            return 0;
        using var cn = _factory.CreateConnection();
        var codigo = NormalizeCode(rawValue);
        var existente = await cn.ExecuteScalarAsync<int?>(
            "SELECT id FROM catalogos WHERE tipo_catalogo = @tipo AND codigo = @codigo LIMIT 1;",
            new { tipo, codigo });
        if (existente.HasValue)
            return existente.Value;

        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO catalogos
            (tipo_catalogo, codigo, nombre, activo, orden, es_default, pendiente_revision)
            VALUES
            (@tipo, @codigo, @nombre, 1, 9999, 0, @pendienteRevision);
            SELECT LAST_INSERT_ID();", new { tipo, codigo, nombre = rawValue.Trim(), pendienteRevision });
    }

    public async Task MergeAsync(int sourceId, int targetId)
    {
        await EnsureSchemaAsync();
        if (sourceId == targetId)
            return;
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE catalogos SET activo = 0 WHERE id = @sourceId;", new { sourceId });
    }

    private static string NormalizeCode(string value)
    {
        var clean = (value ?? "").Trim().ToUpperInvariant();
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", "_");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^A-Z0-9_]", "");
        return string.IsNullOrWhiteSpace(clean) ? "VALOR" : clean;
    }
}
