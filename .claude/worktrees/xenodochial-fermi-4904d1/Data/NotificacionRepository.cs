using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class NotificacionRepository
{
    private readonly DbConnectionFactory _factory;

    public NotificacionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS notificaciones_internas (
                id INT AUTO_INCREMENT PRIMARY KEY,
                usuario_id INT NULL,
                tipo VARCHAR(60) NOT NULL,
                titulo VARCHAR(180) NOT NULL,
                mensaje TEXT NOT NULL,
                entidad_tipo VARCHAR(60) NULL,
                entidad_id INT NULL,
                referencia VARCHAR(160) NOT NULL,
                leida BOOLEAN NOT NULL DEFAULT FALSE,
                fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                fecha_lectura DATETIME NULL,
                UNIQUE KEY uq_notificaciones_referencia (referencia),
                INDEX ix_notificaciones_leida_fecha (leida, fecha_creacion),
                INDEX ix_notificaciones_usuario (usuario_id)
            );");
    }

    public async Task GenerarPendientesAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas (tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            SELECT 'NUEVO_RECLAMO', 'Nuevo reclamo', CONCAT('Reclamo pendiente de ', IFNULL(asegurado, 'cliente sin nombre')), 'RECLAMO', id, CONCAT('reclamo:', id)
            FROM reclamos_whatsapp
            WHERE estado IN ('PENDIENTE', 'PENDIENTE_ENVIO')
            ORDER BY fecha_creacion DESC
            LIMIT 50;");

        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas (tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            SELECT 'PAGO_VENCIDO', 'Pago vencido', CONCAT(c.nombre, ' tiene una cuota vencida de L ', FORMAT(pc.monto, 2)), 'PAGO', pc.id, CONCAT('pago-vencido:', pc.id)
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE pc.estado = 'VENCIDA'
            ORDER BY pc.fecha_vencimiento ASC
            LIMIT 100;");

        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas (tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            SELECT 'POLIZA_POR_VENCER', 'Poliza por vencer', CONCAT(c.nombre, ' vence el ', DATE_FORMAT(p.hasta, '%d/%m/%Y')), 'POLIZA', p.id, CONCAT('poliza-vencer:', p.id)
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE p.activo = 1 AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)
            ORDER BY p.hasta ASC
            LIMIT 100;");

        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas (tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            SELECT 'TALLER_DETECTADO', 'Taller detectado', CONCAT('Pendiente de aprobar: ', nombre), 'TALLER', id, CONCAT('taller-detectado:', id)
            FROM talleres_detectados
            WHERE estado = 'PENDIENTE'
            ORDER BY fecha_creacion DESC
            LIMIT 50;");

        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas (tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            SELECT 'ERROR_AUTOMATIZACION', 'Error de automatizacion', LEFT(mensaje, 220), 'AUTOMATIZACION', automatizacion_id, CONCAT('auto-error:', id)
            FROM automatizacion_logs
            WHERE resultado = 'ERROR'
            ORDER BY fecha DESC
            LIMIT 50;");
    }

    public async Task CrearAsync(string tipo, string titulo, string mensaje, string? entidadTipo, int? entidadId, string referencia, int? usuarioId = null)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            INSERT IGNORE INTO notificaciones_internas
            (usuario_id, tipo, titulo, mensaje, entidad_tipo, entidad_id, referencia)
            VALUES
            (@usuarioId, @tipo, @titulo, @mensaje, @entidadTipo, @entidadId, @referencia);",
            new
            {
                usuarioId,
                tipo,
                titulo,
                mensaje = mensaje.Length > 1000 ? mensaje[..1000] : mensaje,
                entidadTipo = entidadTipo?.Trim().ToUpperInvariant(),
                entidadId,
                referencia
            });
    }

    public async Task<(IEnumerable<NotificacionInterna> items, int unread)> GetAsync(int? usuarioId, int limit = 20)
    {
        await GenerarPendientesAsync();
        using var cn = _factory.CreateConnection();
        var items = await cn.QueryAsync<NotificacionInterna>(@"
            SELECT id Id, usuario_id UsuarioId, tipo Tipo, titulo Titulo, mensaje Mensaje,
                   entidad_tipo EntidadTipo, entidad_id EntidadId, referencia Referencia,
                   leida Leida, fecha_creacion FechaCreacion, fecha_lectura FechaLectura
            FROM notificaciones_internas
            WHERE usuario_id IS NULL OR usuario_id = @usuarioId
            ORDER BY leida ASC, fecha_creacion DESC
            LIMIT @limit;", new { usuarioId, limit });

        var unread = await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM notificaciones_internas
            WHERE leida = 0
              AND (usuario_id IS NULL OR usuario_id = @usuarioId);", new { usuarioId });

        return (items, unread);
    }

    public async Task MarcarLeidaAsync(int id, int? usuarioId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE notificaciones_internas
            SET leida = 1,
                fecha_lectura = NOW()
            WHERE id = @id
              AND (usuario_id IS NULL OR usuario_id = @usuarioId);", new { id, usuarioId });
    }
}
