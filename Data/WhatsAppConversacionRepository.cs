using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class WhatsAppConversacionRepository
{
    private readonly DbConnectionFactory _factory;

    public WhatsAppConversacionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS whatsapp_conversaciones (
                id                  INT          AUTO_INCREMENT PRIMARY KEY,
                telefono            VARCHAR(50)  NOT NULL,
                nombre_contacto     VARCHAR(180) NULL,
                cliente_id          INT          NULL,
                reclamo_id          INT          NULL,
                estado              VARCHAR(30)  NOT NULL DEFAULT 'abierta',
                ultima_actividad    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                no_leidos           INT          NOT NULL DEFAULT 0,
                agente_asignado_id  INT          NULL,
                creado_en           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uq_wa_conv_telefono (telefono),
                INDEX ix_wa_conv_estado        (estado),
                INDEX ix_wa_conv_actividad     (ultima_actividad DESC),
                INDEX ix_wa_conv_cliente       (cliente_id)
            )");

        // Agregar reclamo_id si no existe (migración idempotente)
        await cn.ExecuteAsync(@"
            ALTER TABLE whatsapp_conversaciones
                ADD COLUMN IF NOT EXISTS reclamo_id INT NULL AFTER cliente_id");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS whatsapp_mensajes (
                id                  INT          AUTO_INCREMENT PRIMARY KEY,
                conversacion_id     INT          NOT NULL,
                whatsapp_message_id VARCHAR(200) NULL,
                direccion           VARCHAR(10)  NOT NULL,
                tipo_contenido      VARCHAR(20)  NOT NULL DEFAULT 'texto',
                contenido           TEXT         NULL,
                media_id            VARCHAR(200) NULL,
                media_url           VARCHAR(500) NULL,
                media_tipo_mime     VARCHAR(100) NULL,
                media_nombre        VARCHAR(200) NULL,
                estado              VARCHAR(20)  NOT NULL DEFAULT 'recibido',
                usuario_id          INT          NULL,
                creado_en           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX ix_wa_msg_conv  (conversacion_id),
                INDEX ix_wa_msg_wa_id (whatsapp_message_id),
                CONSTRAINT fk_wa_msg_conv FOREIGN KEY (conversacion_id)
                    REFERENCES whatsapp_conversaciones(id) ON DELETE CASCADE
            )");

        await LimpiarReclamosRotosAsync();
    }

    // ─── Conversaciones ───────────────────────────────────────────────────────

    public async Task<int> GetOrCreateConversacionAsync(string telefono, string? nombre)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            INSERT INTO whatsapp_conversaciones (telefono, nombre_contacto, ultima_actividad)
            VALUES (@telefono, @nombre, NOW())
            ON DUPLICATE KEY UPDATE
                nombre_contacto  = COALESCE(@nombre, nombre_contacto),
                estado           = 'abierta',
                ultima_actividad = NOW()",
            new { telefono, nombre });

        return await cn.ExecuteScalarAsync<int>(
            "SELECT id FROM whatsapp_conversaciones WHERE telefono = @telefono",
            new { telefono });
    }

    public async Task<(IEnumerable<ConversacionListItem> items, int total)> GetConversacionesAsync(
        string? estado, string? buscar, int limit = 50, int offset = 0)
    {
        using var cn = _factory.CreateConnection();

        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(estado) && estado != "todas")
        {
            where.Add("c.estado = @estado");
            p.Add("estado", estado);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var text = buscar.Trim();
            var compact = CompactSearch(text);
            where.Add(@"(
                c.nombre_contacto LIKE @buscar
                OR c.telefono LIKE @buscar
                OR cl.nombre LIKE @buscar
                OR r.conductor LIKE @buscar
                OR r.asegurado LIKE @buscar
                OR r.reclamo LIKE @buscar
                OR r.numero_reclamo LIKE @buscar
                OR r.placa LIKE @buscar
                OR r.poliza LIKE @buscar
                OR r.celular LIKE @buscar
                OR REPLACE(REPLACE(REPLACE(COALESCE(c.telefono, ''), '-', ''), ' ', ''), '+', '') LIKE @compact
                OR REPLACE(REPLACE(REPLACE(COALESCE(r.celular, ''), '-', ''), ' ', ''), '+', '') LIKE @compact
                OR REPLACE(REPLACE(UPPER(COALESCE(r.placa, '')), '-', ''), ' ', '') LIKE @compact
                OR REPLACE(REPLACE(UPPER(COALESCE(r.reclamo, '')), '-', ''), ' ', '') LIKE @compact
                OR REPLACE(REPLACE(UPPER(COALESCE(r.numero_reclamo, '')), '-', ''), ' ', '') LIKE @compact
            )");
            p.Add("buscar", $"%{text}%");
            p.Add("compact", $"%{compact}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        p.Add("limit", limit);
        p.Add("offset", offset);

        var sql = $@"
            SELECT
                c.id                Id,
                c.telefono          Telefono,
                c.nombre_contacto   NombreContacto,
                c.cliente_id        ClienteId,
                cl.nombre           NombreCliente,
                CASE WHEN r.id IS NULL THEN NULL ELSE c.reclamo_id END ReclamoId,
                r.numero_reclamo    NumeroReclamo,
                c.agente_asignado_id AgenteAsignadoId,
                u.Username          AgenteNombre,
                c.estado            Estado,
                c.ultima_actividad  UltimaActividad,
                c.no_leidos         NoLeidos,
                (
                    SELECT m.contenido
                    FROM whatsapp_mensajes m
                    WHERE m.conversacion_id = c.id
                    ORDER BY m.creado_en DESC LIMIT 1
                ) UltimoMensaje,
                (
                    SELECT m.direccion
                    FROM whatsapp_mensajes m
                    WHERE m.conversacion_id = c.id
                    ORDER BY m.creado_en DESC LIMIT 1
                ) UltimoDireccion,
                (
                    SELECT m.tipo_contenido
                    FROM whatsapp_mensajes m
                    WHERE m.conversacion_id = c.id
                    ORDER BY m.creado_en DESC LIMIT 1
                ) UltimoTipoContenido
            FROM whatsapp_conversaciones c
            LEFT JOIN clientes cl ON cl.id = c.cliente_id
            LEFT JOIN reclamos_whatsapp r ON r.id = c.reclamo_id
            LEFT JOIN Users u ON u.Id = c.agente_asignado_id
            {whereSql}
            ORDER BY c.ultima_actividad DESC
            LIMIT @limit OFFSET @offset";

        var countSql = $@"
            SELECT COUNT(*)
            FROM whatsapp_conversaciones c
            LEFT JOIN clientes cl ON cl.id = c.cliente_id
            LEFT JOIN reclamos_whatsapp r ON r.id = c.reclamo_id
            {whereSql}";

        var items = await cn.QueryAsync<ConversacionListItem>(sql, p);
        var total = await cn.ExecuteScalarAsync<int>(countSql, p);
        return (items, total);
    }

    public async Task<int> GetTotalNoLeidosAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(SUM(no_leidos), 0) FROM whatsapp_conversaciones WHERE estado != 'resuelta'");
    }

    public async Task<ConversacionDetalle?> GetConversacionByIdAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryFirstOrDefaultAsync<ConversacionDetalle>(@"
            SELECT
                c.id                Id,
                c.telefono          Telefono,
                c.nombre_contacto   NombreContacto,
                c.cliente_id        ClienteId,
                cl.nombre           NombreCliente,
                CASE WHEN r.id IS NULL THEN NULL ELSE c.reclamo_id END ReclamoId,
                r.numero_reclamo    NumeroReclamo,
                r.conductor         ConductorReclamo,
                c.agente_asignado_id AgenteAsignadoId,
                u.Username          AgenteNombre,
                c.estado            Estado,
                c.ultima_actividad  UltimaActividad,
                c.no_leidos         NoLeidos,
                c.creado_en         CreadoEn
            FROM whatsapp_conversaciones c
            LEFT JOIN clientes cl ON cl.id = c.cliente_id
            LEFT JOIN reclamos_whatsapp r ON r.id = c.reclamo_id
            LEFT JOIN Users u ON u.Id = c.agente_asignado_id
            WHERE c.id = @id",
            new { id });
    }

    public async Task CambiarEstadoAsync(int conversacionId, string estado)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "UPDATE whatsapp_conversaciones SET estado = @estado WHERE id = @id",
            new { estado, id = conversacionId });
    }

    public async Task MarcarLeidoAsync(int conversacionId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "UPDATE whatsapp_conversaciones SET no_leidos = 0 WHERE id = @id",
            new { id = conversacionId });
    }

    public async Task AsociarClienteAsync(int conversacionId, int clienteId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "UPDATE whatsapp_conversaciones SET cliente_id = @clienteId WHERE id = @id",
            new { clienteId, id = conversacionId });
    }

    public async Task AsociarReclamoAsync(int conversacionId, int? reclamoId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            @"
            UPDATE whatsapp_conversaciones
            SET reclamo_id = CASE
                WHEN @reclamoId IS NULL THEN NULL
                WHEN EXISTS (SELECT 1 FROM reclamos_whatsapp WHERE id = @reclamoId) THEN @reclamoId
                ELSE NULL
            END
            WHERE id = @id",
            new { reclamoId, id = conversacionId });
    }

    public async Task<int> LimpiarReclamosRotosAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteAsync(@"
            UPDATE whatsapp_conversaciones c
            LEFT JOIN reclamos_whatsapp r ON r.id = c.reclamo_id
            SET c.reclamo_id = NULL
            WHERE c.reclamo_id IS NOT NULL
              AND r.id IS NULL");
    }

    public async Task AsignarAgenteAsync(int conversacionId, int? agenteId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "UPDATE whatsapp_conversaciones SET agente_asignado_id = @agenteId WHERE id = @id",
            new { agenteId, id = conversacionId });
    }

    // ─── Mensajes ─────────────────────────────────────────────────────────────

    public async Task<int> SaveMensajeAsync(WhatsAppMensaje msg)
    {
        using var cn = _factory.CreateConnection();

        var id = await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO whatsapp_mensajes
                (conversacion_id, whatsapp_message_id, direccion, tipo_contenido,
                 contenido, media_id, media_url, media_tipo_mime, media_nombre,
                 estado, usuario_id, creado_en)
            VALUES
                (@ConversacionId, @WhatsappMessageId, @Direccion, @TipoContenido,
                 @Contenido, @MediaId, @MediaUrl, @MediaTipoMime, @MediaNombre,
                 @Estado, @UsuarioId, NOW());
            SELECT LAST_INSERT_ID();", msg);

        if (msg.Direccion == "entrante")
        {
            await cn.ExecuteAsync(@"
                UPDATE whatsapp_conversaciones
                SET ultima_actividad = NOW(),
                    estado = 'abierta',
                    no_leidos = no_leidos + 1
                WHERE id = @id",
                new { id = msg.ConversacionId });
        }
        else
        {
            await cn.ExecuteAsync(@"
                UPDATE whatsapp_conversaciones SET ultima_actividad = NOW() WHERE id = @id",
                new { id = msg.ConversacionId });
        }

        return id;
    }

    public async Task<(IEnumerable<MensajeDto> items, int total)> GetMensajesAsync(
        int conversacionId, int limit = 50, int offset = 0)
    {
        using var cn = _factory.CreateConnection();

        var p = new DynamicParameters();
        p.Add("convId", conversacionId);
        p.Add("limit", limit);
        p.Add("offset", offset);

        var total = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM whatsapp_mensajes WHERE conversacion_id = @convId", p);

        // Paginado inverso: últimos <limit> mensajes ordenados ASC para mostrar cronológicamente
        var sql = @"
            SELECT * FROM (
                SELECT
                    m.id              Id,
                    m.direccion       Direccion,
                    m.tipo_contenido  TipoContenido,
                    m.contenido       Contenido,
                    m.media_id        MediaId,
                    m.media_tipo_mime MediaTipoMime,
                    m.media_nombre    MediaNombre,
                    m.estado          Estado,
                    u.Username        NombreUsuario,
                    m.creado_en       CreadoEn
                FROM whatsapp_mensajes m
                LEFT JOIN Users u ON u.Id = m.usuario_id
                WHERE m.conversacion_id = @convId
                ORDER BY m.creado_en DESC
                LIMIT @limit OFFSET @offset
            ) sub
            ORDER BY sub.CreadoEn ASC";

        var items = await cn.QueryAsync<MensajeDto>(sql, p);
        return (items, total);
    }

    public async Task<WhatsAppMensaje?> GetMensajeByIdAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryFirstOrDefaultAsync<WhatsAppMensaje>(@"
            SELECT
                id Id,
                conversacion_id ConversacionId,
                whatsapp_message_id WhatsappMessageId,
                direccion Direccion,
                tipo_contenido TipoContenido,
                contenido Contenido,
                media_id MediaId,
                media_url MediaUrl,
                media_tipo_mime MediaTipoMime,
                media_nombre MediaNombre,
                estado Estado,
                usuario_id UsuarioId,
                creado_en CreadoEn
            FROM whatsapp_mensajes
            WHERE id = @id",
            new { id });
    }

    public async Task ActualizarEstadoMensajeAsync(string whatsappMessageId, string estado)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE whatsapp_mensajes SET estado = @estado
            WHERE whatsapp_message_id = @msgId",
            new { estado, msgId = whatsappMessageId });
    }

    // ─── Agentes disponibles ──────────────────────────────────────────────────

    public async Task<IEnumerable<AgenteSummary>> GetAgentesAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<AgenteSummary>(@"
            SELECT u.Id, u.Username, ro.Name RoleName
            FROM Users u
            LEFT JOIN Roles ro ON ro.Id = u.RoleId
            WHERE u.IsActive = 1
            ORDER BY u.Username");
    }

    private static string CompactSearch(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
