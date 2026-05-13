using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class ReclamoRepository
{
    private readonly DbConnectionFactory _factory;

    public ReclamoRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<ReclamoWhatsApp>> GetAllAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                id Id, message_id MessageId, asunto Asunto, aseguradora Aseguradora, asegurado Asegurado,
                poliza Poliza, placa Placa, reclamo Reclamo, conductor Conductor,
                celular Celular, fecha_notificacion FechaNotificacion,
                lugar_accidente LugarAccidente, mensaje_whatsapp MensajeWhatsApp,
                cliente_id ClienteId, poliza_id PolizaId, numero_reclamo NumeroReclamo,
                fecha_reclamo FechaReclamo, tipo_reclamo TipoReclamo, estado_reclamo EstadoReclamo,
                taller_sugerido_id TallerSugeridoId, taller_asignado_id TallerAsignadoId,
                ciudad_detectada CiudadDetectada, motivo_sugerencia_taller MotivoSugerenciaTaller,
                descripcion Descripcion, monto_estimado MontoEstimado, monto_aprobado MontoAprobado, monto_pagado MontoPagado,
                correo_aseguradora_principal CorreoAseguradoraPrincipal, correo_aseguradora_copia CorreoAseguradoraCopia,
                respuesta_aseguradora RespuestaAseguradora, fecha_respuesta_aseguradora FechaRespuestaAseguradora,
                aseguradora_aprobado AseguradoraAprobado,
                actualizado_en ActualizadoEn,
                estado Estado, fecha_creacion FechaCreacion, fecha_envio FechaEnvio,
                error Error
            FROM reclamos_whatsapp
            ORDER BY id DESC;";

        return await cn.QueryAsync<ReclamoWhatsApp>(sql);
    }

    public async Task<ReclamoWhatsApp?> GetByIdAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                id Id, message_id MessageId, asunto Asunto, aseguradora Aseguradora, asegurado Asegurado,
                poliza Poliza, placa Placa, reclamo Reclamo, conductor Conductor,
                celular Celular, fecha_notificacion FechaNotificacion,
                lugar_accidente LugarAccidente, mensaje_whatsapp MensajeWhatsApp,
                cliente_id ClienteId, poliza_id PolizaId, numero_reclamo NumeroReclamo,
                fecha_reclamo FechaReclamo, tipo_reclamo TipoReclamo, estado_reclamo EstadoReclamo,
                taller_sugerido_id TallerSugeridoId, taller_asignado_id TallerAsignadoId,
                ciudad_detectada CiudadDetectada, motivo_sugerencia_taller MotivoSugerenciaTaller,
                descripcion Descripcion, monto_estimado MontoEstimado, monto_aprobado MontoAprobado, monto_pagado MontoPagado,
                correo_aseguradora_principal CorreoAseguradoraPrincipal, correo_aseguradora_copia CorreoAseguradoraCopia,
                respuesta_aseguradora RespuestaAseguradora, fecha_respuesta_aseguradora FechaRespuestaAseguradora,
                aseguradora_aprobado AseguradoraAprobado,
                actualizado_en ActualizadoEn,
                estado Estado, fecha_creacion FechaCreacion, fecha_envio FechaEnvio,
                error Error
            FROM reclamos_whatsapp
            WHERE id = @id;";

        return await cn.QueryFirstOrDefaultAsync<ReclamoWhatsApp>(sql, new { id });
    }

    public async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT COUNT(1)
            FROM reclamos_whatsapp
            WHERE message_id = @messageId;";

        return await cn.ExecuteScalarAsync<int>(sql, new { messageId }) > 0;
    }

    public async Task<bool> ExistsByClaimReferenceAsync(string? reclamo, string? placa)
    {
        if (string.IsNullOrWhiteSpace(reclamo))
            return false;

        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT COUNT(1)
            FROM reclamos_whatsapp
            WHERE UPPER(COALESCE(reclamo, numero_reclamo, '')) = UPPER(@reclamo)
              AND (
                    @placa IS NULL
                    OR @placa = ''
                    OR UPPER(COALESCE(placa, '')) = UPPER(@placa)
                  );";

        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            reclamo = reclamo.Trim(),
            placa = placa?.Trim() ?? ""
        }) > 0;
    }

    public async Task<int> InsertAsync(ReclamoWhatsApp r)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO reclamos_whatsapp
            (
                message_id, asunto, aseguradora, asegurado, poliza, placa, reclamo, conductor,
                celular, fecha_notificacion, lugar_accidente, mensaje_whatsapp, estado,
                cliente_id, poliza_id, numero_reclamo, fecha_reclamo, tipo_reclamo, estado_reclamo,
                taller_sugerido_id, taller_asignado_id, ciudad_detectada, motivo_sugerencia_taller,
                descripcion, monto_estimado, monto_aprobado, monto_pagado, actualizado_en
            )
            VALUES
            (
                @MessageId, @Asunto, @Aseguradora, @Asegurado, @Poliza, @Placa, @Reclamo, @Conductor,
                @Celular, @FechaNotificacion, @LugarAccidente, @MensajeWhatsApp, @Estado,
                @ClienteId, @PolizaId, @NumeroReclamo, @FechaReclamo, @TipoReclamo, @EstadoReclamo,
                @TallerSugeridoId, @TallerAsignadoId, @CiudadDetectada, @MotivoSugerenciaTaller,
                @Descripcion, @MontoEstimado, @MontoAprobado, @MontoPagado, @ActualizadoEn
            );

            SELECT LAST_INSERT_ID();";

        if (string.IsNullOrWhiteSpace(r.MessageId))
            r.MessageId = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(r.Estado))
            r.Estado = "PENDIENTE";

        var id = await cn.ExecuteScalarAsync<int>(sql, r);

        await CrearDocumentosInicialesAsync(id);

        return id;
    }

    public async Task CrearDocumentosInicialesAsync(int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var documentos = new[]
        {
            "Aviso de accidente",
            "Certificacion de transito",
            "Tarjeta de identidad del conductor",
            "Licencia del conductor",
            "Boleta de circulacion",
            "2 cotizaciones de talleres"
        };

        const string existeSql = @"
            SELECT COUNT(1)
            FROM reclamo_documentos
            WHERE reclamo_id = @reclamoId;";

        var yaExisten = await cn.ExecuteScalarAsync<int>(existeSql, new { reclamoId });

        if (yaExisten > 0)
            return;

        const string insertSql = @"
            INSERT INTO reclamo_documentos
            (reclamo_id, documento, recibido)
            VALUES
            (@ReclamoId, @Documento, 0);";

        foreach (var documento in documentos)
        {
            await cn.ExecuteAsync(insertSql, new
            {
                ReclamoId = reclamoId,
                Documento = documento
            });
        }
    }

    public async Task<IEnumerable<ReclamoDocumento>> GetDocumentosAsync(int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                id Id,
                reclamo_id ReclamoId,
                documento Documento,
                recibido Recibido,
                fecha_recibido FechaRecibido
            FROM reclamo_documentos
            WHERE reclamo_id = @reclamoId
            ORDER BY id;";

        return await cn.QueryAsync<ReclamoDocumento>(sql, new { reclamoId });
    }

    public async Task AgregarDocumentoPendienteSiNoExisteAsync(int reclamoId, string documento)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO reclamo_documentos (reclamo_id, documento, recibido)
            SELECT @reclamoId, @documento, 0
            WHERE NOT EXISTS (
                SELECT 1
                FROM reclamo_documentos
                WHERE reclamo_id = @reclamoId
                  AND LOWER(documento) = LOWER(@documento)
            );";

        await cn.ExecuteAsync(sql, new { reclamoId, documento = documento.Trim() });
    }

    public async Task ActualizarDocumentoAsync(int documentoId, int reclamoId, bool recibido)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamo_documentos
            SET recibido = @recibido,
                fecha_recibido = CASE 
                                    WHEN @recibido = 1 THEN NOW()
                                    ELSE NULL
                                 END
            WHERE id = @documentoId
              AND reclamo_id = @reclamoId;";

        await cn.ExecuteAsync(sql, new { documentoId, reclamoId, recibido });
    }

    public async Task MarcarTodosDocumentosAsync(int reclamoId, bool recibido)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamo_documentos
            SET recibido = @recibido,
                fecha_recibido = CASE
                                    WHEN @recibido = 1 THEN NOW()
                                    ELSE NULL
                                 END
            WHERE reclamo_id = @reclamoId;";

        await cn.ExecuteAsync(sql, new { reclamoId, recibido });
    }

    public async Task UpdateCorreosAseguradoraAsync(int id, string? principal, string? copia)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET correo_aseguradora_principal = @principal,
                correo_aseguradora_copia = @copia,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            principal = string.IsNullOrWhiteSpace(principal) ? null : principal.Trim(),
            copia = string.IsNullOrWhiteSpace(copia) ? null : copia.Trim()
        });
    }

    public async Task RegistrarRespuestaAseguradoraAsync(int id, string? respuesta, bool aprobado)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET respuesta_aseguradora = @respuesta,
                fecha_respuesta_aseguradora = NOW(),
                aseguradora_aprobado = @aprobado,
                estado = CASE WHEN @aprobado = 1 THEN 'ASEGURADORA_APROBADO' ELSE 'EN_REVISION_ASEGURADORA' END,
                estado_reclamo = CASE WHEN @aprobado = 1 THEN 'ASEGURADORA_APROBADO' ELSE 'EN_REVISION_ASEGURADORA' END,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            respuesta = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim(),
            aprobado
        });
    }

    public async Task<bool> TodosDocumentosRecibidosAsync(int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT COUNT(1)
            FROM reclamo_documentos
            WHERE reclamo_id = @reclamoId
              AND recibido = 0;";

        var pendientes = await cn.ExecuteScalarAsync<int>(sql, new { reclamoId });

        return pendientes == 0;
    }

    public async Task UpdateEstadoAsync(int id, string estado, string? error = null)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET estado = @estado,
                estado_reclamo = @estado,
                actualizado_en = NOW(),
                error = @error,
                fecha_envio = CASE
                                WHEN @estado = 'ENVIADO' THEN NOW()
                                ELSE fecha_envio
                              END
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new { id, estado, error });
    }

    public async Task UpdateEstadoByMessageIdAsync(string messageId, string estado, string? error = null)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET estado = @estado,
                error = @error,
                fecha_envio = CASE
                                WHEN @estado = 'ENVIADO' THEN NOW()
                                ELSE fecha_envio
                              END
            WHERE message_id = @messageId;";

        await cn.ExecuteAsync(sql, new { messageId, estado, error });
    }

    public async Task<IEnumerable<ReclamoWhatsApp>> GetParaRecordatorioAsync(int diasEntreRecordatorios = 1, int maxRecordatorios = 3)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        diasEntreRecordatorios = Math.Clamp(diasEntreRecordatorios, 1, 365);
        maxRecordatorios = Math.Clamp(maxRecordatorios, 1, 50);

        const string sql = @"
        SELECT
            id Id,
            message_id MessageId,
            asunto Asunto,
            asegurado Asegurado,
            poliza Poliza,
            placa Placa,
            reclamo Reclamo,
            conductor Conductor,
            celular Celular,
            fecha_notificacion FechaNotificacion,
            lugar_accidente LugarAccidente,
            mensaje_whatsapp MensajeWhatsApp,
            estado Estado,
            fecha_creacion FechaCreacion,
            fecha_envio FechaEnvio,
            error Error
        FROM reclamos_whatsapp
        WHERE estado IN ('ENVIADO', 'EN_SEGUIMIENTO')
        AND fecha_creacion < DATE_SUB(NOW(), INTERVAL @diasEntreRecordatorios DAY)
        AND (
            fecha_ultimo_recordatorio IS NULL
            OR fecha_ultimo_recordatorio < DATE_SUB(NOW(), INTERVAL @diasEntreRecordatorios DAY)
        )  AND cantidad_recordatorios < @maxRecordatorios;";

        return await cn.QueryAsync<ReclamoWhatsApp>(sql, new { diasEntreRecordatorios, maxRecordatorios });
    }

    public async Task MarcarRecordatorioAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
        UPDATE reclamos_whatsapp
        SET fecha_ultimo_recordatorio = NOW(),
            cantidad_recordatorios = cantidad_recordatorios + 1
        WHERE id = @id;";

        await cn.ExecuteAsync(sql, new { id });
    }

    public async Task<dynamic> GetDashboardStatsAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
        SELECT
            COUNT(*) Total,
            SUM(CASE WHEN estado IN ('NUEVO','EN_REVISION','DOCUMENTOS_PENDIENTES','EN_TALLER','EN_ASEGURADORA','ENVIADO','EN_SEGUIMIENTO') THEN 1 ELSE 0 END) Pendientes,
            SUM(CASE WHEN estado IN ('CERRADO','COMPLETO') THEN 1 ELSE 0 END) Completos,
            SUM(CASE WHEN estado = 'ERROR' THEN 1 ELSE 0 END) Errores,
            SUM(CASE WHEN estado = 'DOCUMENTOS_PENDIENTES' THEN 1 ELSE 0 END) ConDocumentosPendientes,
            SUM(CASE WHEN estado IN ('CERRADO','COMPLETO') AND MONTH(fecha_creacion) = MONTH(CURDATE()) AND YEAR(fecha_creacion) = YEAR(CURDATE()) THEN 1 ELSE 0 END) CerradosMes,
            COALESCE(SUM(IFNULL(monto_estimado,0)),0) MontoEstimado,
            COALESCE(SUM(IFNULL(monto_aprobado,0)),0) MontoAprobado,
            COALESCE(SUM(IFNULL(monto_pagado,0)),0) MontoPagado
        FROM reclamos_whatsapp;";

        return await cn.QueryFirstAsync(sql);
    }

    public async Task<int> GetDocumentosPendientesAsync(int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
        SELECT COUNT(1)
        FROM reclamo_documentos
        WHERE reclamo_id = @reclamoId
        AND recibido = 0;";

        return await cn.ExecuteScalarAsync<int>(sql, new { reclamoId });
    }

    public async Task<IEnumerable<ReclamoRequisito>> GetRequisitosByTipoAsync(string tipoReclamo)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT id Id, tipo_reclamo TipoReclamo, tipo_documento TipoDocumento, requerido Requerido, activo Activo
            FROM reclamo_requisitos
            WHERE activo = 1
              AND tipo_reclamo = @tipoReclamo
            ORDER BY requerido DESC, tipo_documento ASC;";
        return await cn.QueryAsync<ReclamoRequisito>(sql, new { tipoReclamo = (tipoReclamo ?? "GENERAL").Trim().ToUpperInvariant() });
    }

    public async Task<int> UpsertRequisitoAsync(ReclamoRequisito model)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO reclamo_requisitos (tipo_reclamo, tipo_documento, requerido, activo)
            VALUES (@TipoReclamo, @TipoDocumento, @Requerido, @Activo)
            ON DUPLICATE KEY UPDATE
              requerido = VALUES(requerido),
              activo = VALUES(activo),
              id = LAST_INSERT_ID(id);
            SELECT LAST_INSERT_ID();";
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            TipoReclamo = (model.TipoReclamo ?? "GENERAL").Trim().ToUpperInvariant(),
            TipoDocumento = (model.TipoDocumento ?? "OTRO").Trim().ToUpperInvariant(),
            model.Requerido,
            model.Activo
        });
    }

    public async Task<int> CountFaltantesByEntidadDocumentosAsync(int reclamoId, string tipoReclamo)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1)
            FROM reclamo_requisitos rr
            WHERE rr.activo = 1
              AND rr.requerido = 1
              AND rr.tipo_reclamo = @tipoReclamo
              AND NOT EXISTS (
                  SELECT 1
                  FROM documentos d
                  WHERE d.activo = 1
                    AND d.entidad_tipo = 'RECLAMO'
                    AND d.entidad_id = @reclamoId
                    AND d.tipo_documento = rr.tipo_documento
              );";
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            reclamoId,
            tipoReclamo = (tipoReclamo ?? "GENERAL").Trim().ToUpperInvariant()
        });
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            ALTER TABLE reclamos_whatsapp
                ADD COLUMN IF NOT EXISTS cliente_id INT NULL,
                ADD COLUMN IF NOT EXISTS aseguradora VARCHAR(160) NULL,
                ADD COLUMN IF NOT EXISTS poliza_id INT NULL,
                ADD COLUMN IF NOT EXISTS numero_reclamo VARCHAR(80) NULL,
                ADD COLUMN IF NOT EXISTS fecha_reclamo DATE NULL,
                ADD COLUMN IF NOT EXISTS tipo_reclamo VARCHAR(40) NULL,
                ADD COLUMN IF NOT EXISTS estado_reclamo VARCHAR(40) NULL,
                ADD COLUMN IF NOT EXISTS taller_sugerido_id INT NULL,
                ADD COLUMN IF NOT EXISTS taller_asignado_id INT NULL,
                ADD COLUMN IF NOT EXISTS ciudad_detectada VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS motivo_sugerencia_taller VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS descripcion TEXT NULL,
                ADD COLUMN IF NOT EXISTS monto_estimado DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS monto_aprobado DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS monto_pagado DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS correo_aseguradora_principal VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS correo_aseguradora_copia VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS respuesta_aseguradora TEXT NULL,
                ADD COLUMN IF NOT EXISTS fecha_respuesta_aseguradora DATETIME NULL,
                ADD COLUMN IF NOT EXISTS aseguradora_aprobado TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS actualizado_en DATETIME NULL;
            DELETE FROM reclamo_documentos
            WHERE LOWER(documento) IN ('pago de primas al dia', 'pago de primas al día', 'pago de primas al dÃ­a');
            UPDATE reclamo_documentos SET documento = 'Certificacion de transito' WHERE documento = 'CertificaciÃ³n de trÃ¡nsito';
            UPDATE reclamo_documentos SET documento = 'Boleta de circulacion' WHERE documento = 'Boleta de circulaciÃ³n';
            UPDATE reclamo_documentos SET documento = 'Inspeccion puntual de danos' WHERE documento = 'InspecciÃ³n puntual de daÃ±os';
            UPDATE reclamos_whatsapp
            SET fecha_reclamo = IFNULL(fecha_reclamo, fecha_notificacion),
                tipo_reclamo = IFNULL(tipo_reclamo, 'GENERAL'),
                estado_reclamo = IFNULL(estado_reclamo, estado),
                numero_reclamo = IFNULL(numero_reclamo, reclamo),
                actualizado_en = IFNULL(actualizado_en, fecha_creacion)
            WHERE fecha_reclamo IS NULL
               OR tipo_reclamo IS NULL
               OR estado_reclamo IS NULL
               OR actualizado_en IS NULL;

            CREATE TABLE IF NOT EXISTS reclamo_requisitos (
                id INT AUTO_INCREMENT PRIMARY KEY,
                tipo_reclamo VARCHAR(40) NOT NULL,
                tipo_documento VARCHAR(80) NOT NULL,
                requerido TINYINT(1) NOT NULL DEFAULT 1,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                UNIQUE KEY uq_reclamo_requisito (tipo_reclamo, tipo_documento)
            );

            INSERT INTO reclamo_requisitos (tipo_reclamo, tipo_documento, requerido, activo)
            VALUES
                ('AUTO','FOTO_RECLAMO',1,1),
                ('AUTO','LICENCIA',1,1),
                ('AUTO','TARJETA_CIRCULACION',1,1),
                ('AUTO','COTIZACION_TALLER',1,1)
            ON DUPLICATE KEY UPDATE requerido = VALUES(requerido), activo = VALUES(activo);");
    }
}
