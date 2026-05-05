using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class CorreoRevisionRepository
{
    private readonly DbConnectionFactory _factory;

    public CorreoRevisionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            CREATE TABLE IF NOT EXISTS correo_revision (
                id INT AUTO_INCREMENT PRIMARY KEY,
                message_id VARCHAR(255) NOT NULL,
                subject VARCHAR(500) NOT NULL,
                estado VARCHAR(30) NOT NULL,
                motivo VARCHAR(1000) NOT NULL,
                reclamo_id INT NULL,
                body_preview TEXT NULL,
                fecha_procesamiento_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uq_correo_revision_message_id (message_id),
                INDEX ix_correo_revision_estado_fecha (estado, fecha_procesamiento_utc),
                INDEX ix_correo_revision_fecha (fecha_procesamiento_utc)
            );";

        await cn.ExecuteAsync(sql);
    }

    public async Task UpsertAsync(CorreoProcesamientoDetalle detalle, string? body)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO correo_revision
            (message_id, subject, estado, motivo, reclamo_id, body_preview, fecha_procesamiento_utc)
            VALUES
            (@MessageId, @Subject, @Estado, @Motivo, @ReclamoId, @BodyPreview, UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                subject = VALUES(subject),
                estado = VALUES(estado),
                motivo = VALUES(motivo),
                reclamo_id = VALUES(reclamo_id),
                body_preview = VALUES(body_preview),
                fecha_procesamiento_utc = UTC_TIMESTAMP();";

        await cn.ExecuteAsync(sql, new
        {
            detalle.MessageId,
            detalle.Subject,
            detalle.Estado,
            detalle.Motivo,
            detalle.ReclamoId,
            BodyPreview = Preview(body)
        });
    }

    public async Task<IReadOnlyList<CorreoRevisionItem>> GetAsync(string? estado, int limit = 100)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        limit = Math.Clamp(limit, 1, 300);

        var where = string.IsNullOrWhiteSpace(estado) || string.Equals(estado, "TODOS", StringComparison.OrdinalIgnoreCase)
            ? ""
            : "WHERE estado = @estado";

        var sql = $@"
            SELECT
                id Id,
                message_id MessageId,
                subject Subject,
                estado Estado,
                motivo Motivo,
                reclamo_id ReclamoId,
                COALESCE(body_preview, '') BodyPreview,
                fecha_procesamiento_utc FechaProcesamientoUtc
            FROM correo_revision
            {where}
            ORDER BY fecha_procesamiento_utc DESC, id DESC
            LIMIT @limit;";

        var rows = await cn.QueryAsync<CorreoRevisionItem>(sql, new { estado, limit });
        return rows.ToList();
    }

    private static string Preview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";

        var normalized = body.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        return normalized.Length <= 1200 ? normalized : normalized[..1200];
    }
}
