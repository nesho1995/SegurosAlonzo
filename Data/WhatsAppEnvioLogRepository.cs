using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class WhatsAppEnvioLogRepository
{
    private readonly DbConnectionFactory _factory;

    public WhatsAppEnvioLogRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS whatsapp_envios_log (
                id INT AUTO_INCREMENT PRIMARY KEY,
                referencia VARCHAR(180) NOT NULL,
                entidad_tipo VARCHAR(60) NOT NULL,
                entidad_id INT NULL,
                telefono VARCHAR(50) NULL,
                tipo_evento VARCHAR(80) NOT NULL,
                automatico BOOLEAN NOT NULL DEFAULT TRUE,
                estado VARCHAR(30) NOT NULL,
                mensaje TEXT NULL,
                respuesta TEXT NULL,
                fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uq_whatsapp_envios_referencia (referencia),
                INDEX ix_whatsapp_envios_entidad (entidad_tipo, entidad_id),
                INDEX ix_whatsapp_envios_estado (estado)
            );");
    }

    public async Task<bool> ExistsAsync(string referencia)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM whatsapp_envios_log WHERE referencia = @referencia;",
            new { referencia }) > 0;
    }

    public async Task<string?> GetEstadoAsync(string referencia)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<string?>(
            "SELECT estado FROM whatsapp_envios_log WHERE referencia = @referencia LIMIT 1;",
            new { referencia });
    }

    public async Task RegistrarAsync(string referencia, string entidadTipo, int? entidadId, string? telefono, string tipoEvento, bool automatico, string estado, string? mensaje, string? respuesta)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            INSERT INTO whatsapp_envios_log
            (referencia, entidad_tipo, entidad_id, telefono, tipo_evento, automatico, estado, mensaje, respuesta)
            VALUES
            (@referencia, @entidadTipo, @entidadId, @telefono, @tipoEvento, @automatico, @estado, @mensaje, @respuesta)
            ON DUPLICATE KEY UPDATE
                estado = VALUES(estado),
                respuesta = VALUES(respuesta),
                mensaje = VALUES(mensaje),
                telefono = VALUES(telefono);",
            new
            {
                referencia,
                entidadTipo = entidadTipo.Trim().ToUpperInvariant(),
                entidadId,
                telefono,
                tipoEvento,
                automatico,
                estado,
                mensaje = mensaje is { Length: > 1000 } ? mensaje[..1000] : mensaje,
                respuesta = respuesta is { Length: > 2000 } ? respuesta[..2000] : respuesta
            });
    }

    public async Task<bool> ExistsSuccessfulDuplicateAsync(string entidadTipo, int? entidadId, string tipoEvento, string telefono, string mensajeHash, DateTime? fechaRegla)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT COUNT(1)
            FROM whatsapp_envios_log
            WHERE entidad_tipo = @entidadTipo
              AND entidad_id <=> @entidadId
              AND tipo_evento = @tipoEvento
              AND telefono = @telefono
              AND estado = 'ENVIADO'
              AND (
                    DATE(fecha_creacion) = CURDATE()
                    OR DATE(fecha_creacion) = DATE(@fechaRegla)
                  )
              AND SHA2(IFNULL(mensaje, ''), 256) = @mensajeHash;";

        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            entidadTipo = entidadTipo.Trim().ToUpperInvariant(),
            entidadId,
            tipoEvento,
            telefono,
            mensajeHash,
            fechaRegla = fechaRegla ?? DateTime.UtcNow
        }) > 0;
    }
}
