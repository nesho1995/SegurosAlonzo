using Dapper;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services;

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
                monto_deducible MontoDeducible, monto_rsa MontoRsa, moneda_pagos_finales MonedaPagosFinales,
                estado_deducible EstadoDeducible, estado_rsa EstadoRsa,
                fecha_solicitud_deducible FechaSolicitudDeducible, fecha_solicitud_rsa FechaSolicitudRsa,
                estado_cotizaciones EstadoCotizaciones, cotizaciones_nota CotizacionesNota,
                caso_especial CasoEspecial, caso_especial_nota CasoEspecialNota,
                estado_seguimiento EstadoSeguimiento, fecha_ultima_revision FechaUltimaRevision,
                usuario_ultima_revision_id UsuarioUltimaRevisionId,
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
                monto_deducible MontoDeducible, monto_rsa MontoRsa, moneda_pagos_finales MonedaPagosFinales,
                estado_deducible EstadoDeducible, estado_rsa EstadoRsa,
                fecha_solicitud_deducible FechaSolicitudDeducible, fecha_solicitud_rsa FechaSolicitudRsa,
                estado_cotizaciones EstadoCotizaciones, cotizaciones_nota CotizacionesNota,
                caso_especial CasoEspecial, caso_especial_nota CasoEspecialNota,
                estado_seguimiento EstadoSeguimiento, fecha_ultima_revision FechaUltimaRevision,
                usuario_ultima_revision_id UsuarioUltimaRevisionId,
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
            (reclamo_id, documento, recibido, cantidad_requerida, minimo_aceptable, permite_excepcion)
            VALUES
            (@ReclamoId, @Documento, 0, @CantidadRequerida, @MinimoAceptable, @PermiteExcepcion);";

        foreach (var documento in documentos)
        {
            var esCotizacion = IsCotizacionDocumento(documento);
            await cn.ExecuteAsync(insertSql, new
            {
                ReclamoId = reclamoId,
                Documento = documento,
                CantidadRequerida = esCotizacion ? 2 : 1,
                MinimoAceptable = 1,
                PermiteExcepcion = esCotizacion
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
                fecha_recibido FechaRecibido,
                cantidad_requerida CantidadRequerida,
                minimo_aceptable MinimoAceptable,
                permite_excepcion PermiteExcepcion,
                excepcion_aceptada ExcepcionAceptada,
                excepcion_observacion ExcepcionObservacion,
                (
                    SELECT COUNT(1)
                    FROM documentos d
                    WHERE d.activo = 1
                      AND d.entidad_tipo = 'RECLAMO'
                      AND d.entidad_id = rd.reclamo_id
                      AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                ) AdjuntosRecibidos
            FROM reclamo_documentos rd
            WHERE rd.reclamo_id = @reclamoId
            ORDER BY rd.id;";

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
        if (IsCotizacionDocumento(documento))
        {
            await cn.ExecuteAsync(@"
                UPDATE reclamo_documentos
                SET cantidad_requerida = 2,
                    minimo_aceptable = 1,
                    permite_excepcion = 1
                WHERE reclamo_id = @reclamoId
                  AND LOWER(documento) = LOWER(@documento);",
                new { reclamoId, documento = documento.Trim() });
        }
    }

    public async Task ActualizarDocumentoSegunAdjuntosAsync(int documentoId, int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamo_documentos rd
            SET recibido = CASE
                    WHEN (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.cantidad_requerida THEN 1
                    WHEN rd.excepcion_aceptada = 1 THEN 1
                    ELSE 0
                END,
                fecha_recibido = CASE
                    WHEN (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.cantidad_requerida OR rd.excepcion_aceptada = 1 THEN IFNULL(rd.fecha_recibido, NOW())
                    ELSE NULL
                END
            WHERE rd.id = @documentoId
              AND rd.reclamo_id = @reclamoId;";

        await cn.ExecuteAsync(sql, new { documentoId, reclamoId });
    }

    public async Task RecalcularDocumentoPorTipoAsync(int reclamoId, string tipoDocumento)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamo_documentos rd
            SET recibido = CASE
                    WHEN (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.cantidad_requerida THEN 1
                    WHEN rd.excepcion_aceptada = 1 AND (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.minimo_aceptable THEN 1
                    ELSE 0
                END,
                fecha_recibido = CASE
                    WHEN (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.cantidad_requerida
                    OR (rd.excepcion_aceptada = 1 AND (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) >= rd.minimo_aceptable) THEN IFNULL(rd.fecha_recibido, NOW())
                    ELSE NULL
                END,
                excepcion_aceptada = CASE
                    WHEN rd.excepcion_aceptada = 1 AND (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) < rd.minimo_aceptable THEN 0
                    ELSE rd.excepcion_aceptada
                END,
                excepcion_observacion = CASE
                    WHEN rd.excepcion_aceptada = 1 AND (
                        SELECT COUNT(1)
                        FROM documentos d
                        WHERE d.activo = 1
                          AND d.entidad_tipo = 'RECLAMO'
                          AND d.entidad_id = rd.reclamo_id
                          AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                    ) < rd.minimo_aceptable THEN NULL
                    ELSE rd.excepcion_observacion
                END
            WHERE rd.reclamo_id = @reclamoId
              AND UPPER(rd.documento) = UPPER(@tipoDocumento);";

        await cn.ExecuteAsync(sql, new { reclamoId, tipoDocumento });
    }

    public async Task<(bool ok, string response)> AceptarDocumentoConExcepcionAsync(int documentoId, int reclamoId, string? observacion)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var doc = await cn.QueryFirstOrDefaultAsync<ReclamoDocumento>(@"
            SELECT
                rd.id Id,
                rd.reclamo_id ReclamoId,
                rd.documento Documento,
                rd.recibido Recibido,
                rd.fecha_recibido FechaRecibido,
                rd.cantidad_requerida CantidadRequerida,
                rd.minimo_aceptable MinimoAceptable,
                rd.permite_excepcion PermiteExcepcion,
                rd.excepcion_aceptada ExcepcionAceptada,
                rd.excepcion_observacion ExcepcionObservacion,
                (
                    SELECT COUNT(1)
                    FROM documentos d
                    WHERE d.activo = 1
                      AND d.entidad_tipo = 'RECLAMO'
                      AND d.entidad_id = rd.reclamo_id
                      AND UPPER(d.tipo_documento) = UPPER(rd.documento)
                ) AdjuntosRecibidos
            FROM reclamo_documentos rd
            WHERE rd.id = @documentoId
              AND rd.reclamo_id = @reclamoId;",
            new { documentoId, reclamoId });

        if (doc is null)
            return (false, "Documento del checklist no encontrado.");
        if (!doc.PermiteExcepcion)
            return (false, "Este documento no permite cierre con excepcion.");
        if (doc.AdjuntosRecibidos < doc.MinimoAceptable)
            return (false, $"Se requiere al menos {doc.MinimoAceptable} adjunto antes de aceptar excepcion.");
        if (string.IsNullOrWhiteSpace(observacion))
            return (false, "Ingresa el motivo de la excepcion.");

        await cn.ExecuteAsync(@"
            UPDATE reclamo_documentos
            SET recibido = 1,
                fecha_recibido = IFNULL(fecha_recibido, NOW()),
                excepcion_aceptada = 1,
                excepcion_observacion = @observacion
            WHERE id = @documentoId
              AND reclamo_id = @reclamoId;",
            new { documentoId, reclamoId, observacion = observacion.Trim() });

        return (true, "Documento aceptado con excepcion.");
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

    public async Task UpdateDatosBasicosAsync(int id, string? poliza, string? reclamo, string? placa, string? celular, string? ciudad)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET poliza = @poliza,
                reclamo = @reclamo,
                numero_reclamo = @reclamo,
                placa = @placa,
                celular = @celular,
                ciudad_detectada = @ciudad,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            poliza = string.IsNullOrWhiteSpace(poliza) ? null : poliza.Trim().ToUpperInvariant().Replace(" ", ""),
            reclamo = string.IsNullOrWhiteSpace(reclamo) ? null : reclamo.Trim().ToUpperInvariant(),
            placa = string.IsNullOrWhiteSpace(placa) ? null : placa.Trim().ToUpperInvariant(),
            celular = string.IsNullOrWhiteSpace(celular) ? null : celular.Trim(),
            ciudad = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim().ToUpperInvariant()
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

    public async Task<int> RegistrarRespuestaAseguradoraHistorialAsync(
        int reclamoId,
        string origen,
        string? remitente,
        string? asunto,
        string? respuesta,
        InsuranceResponseAnalysis analysis,
        string? acciones,
        int? usuarioId)
    {
        await EnsureSchemaAsync();
        if (string.IsNullOrWhiteSpace(respuesta))
            return 0;

        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO reclamo_respuestas_aseguradora
            (
                reclamo_id, origen, remitente, asunto, respuesta, aprobado,
                requiere_rsa, requiere_deducible, monto_rsa, monto_deducible, solicita_mas_documentos,
                aprobado_sin_pagos_finales, acciones, usuario_id
            )
            VALUES
            (
                @reclamoId, @origen, @remitente, @asunto, @respuesta, @aprobado,
                @requiereRsa, @requiereDeducible, @montoRsa, @montoDeducible, @solicitaMasDocumentos,
                @aprobadoSinPagosFinales, @acciones, @usuarioId
            );
            SELECT LAST_INSERT_ID();";

        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            reclamoId,
            origen = string.IsNullOrWhiteSpace(origen) ? "MANUAL" : origen.Trim().ToUpperInvariant(),
            remitente = string.IsNullOrWhiteSpace(remitente) ? null : remitente.Trim(),
            asunto = string.IsNullOrWhiteSpace(asunto) ? null : asunto.Trim(),
            respuesta = respuesta.Trim(),
            aprobado = analysis.Aprobado,
            requiereRsa = analysis.RequiereRsa,
            requiereDeducible = analysis.RequiereDeducible,
            montoRsa = analysis.MontoRsa,
            montoDeducible = analysis.MontoDeducible,
            solicitaMasDocumentos = analysis.SolicitaMasDocumentos,
            aprobadoSinPagosFinales = analysis.AprobadoSinPagosFinales,
            acciones,
            usuarioId
        });
    }

    public async Task AplicarAnalisisAseguradoraAsync(int id, InsuranceResponseAnalysis analysis)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET moneda_pagos_finales = 'LPS',
                monto_deducible = CASE
                    WHEN @requiereDeducible = 1 AND @montoDeducible IS NOT NULL THEN @montoDeducible
                    ELSE monto_deducible
                END,
                monto_rsa = CASE
                    WHEN @requiereRsa = 1 AND @montoRsa IS NOT NULL THEN @montoRsa
                    ELSE monto_rsa
                END,
                estado_deducible = CASE
                    WHEN @requiereDeducible = 1 AND @montoDeducible IS NOT NULL THEN 'PENDIENTE_PAGO'
                    WHEN @requiereDeducible = 1 THEN 'PENDIENTE_MONTO'
                    ELSE estado_deducible
                END,
                estado_rsa = CASE
                    WHEN @requiereRsa = 1 AND @montoRsa IS NOT NULL THEN 'PENDIENTE_PAGO'
                    WHEN @requiereRsa = 1 THEN 'PENDIENTE_MONTO'
                    ELSE estado_rsa
                END,
                fecha_solicitud_deducible = CASE
                    WHEN @requiereDeducible = 1 THEN IFNULL(fecha_solicitud_deducible, NOW())
                    ELSE fecha_solicitud_deducible
                END,
                fecha_solicitud_rsa = CASE
                    WHEN @requiereRsa = 1 THEN IFNULL(fecha_solicitud_rsa, NOW())
                    ELSE fecha_solicitud_rsa
                END,
                estado_seguimiento = CASE
                    WHEN @requiereDeducible = 1 OR @requiereRsa = 1 THEN 'ESPERANDO_CLIENTE'
                    WHEN @solicitaMasDocumentos = 1 THEN 'ESPERANDO_CLIENTE'
                    WHEN @aprobado = 1 THEN 'LISTO'
                    ELSE estado_seguimiento
                END,
                fecha_ultima_revision = NOW(),
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            requiereDeducible = analysis.RequiereDeducible,
            requiereRsa = analysis.RequiereRsa,
            montoDeducible = analysis.MontoDeducible,
            montoRsa = analysis.MontoRsa,
            solicitaMasDocumentos = analysis.SolicitaMasDocumentos,
            aprobado = analysis.Aprobado
        });
    }

    public async Task RegistrarRespuestaAseguradoraCorreoAsync(int id, string? respuesta, bool aprobado)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE reclamos_whatsapp
            SET respuesta_aseguradora = CASE
                    WHEN @respuesta IS NULL THEN respuesta_aseguradora
                    WHEN respuesta_aseguradora IS NULL OR respuesta_aseguradora = '' THEN @respuesta
                    ELSE CONCAT(respuesta_aseguradora, '\n\n---\n', @respuesta)
                END,
                fecha_respuesta_aseguradora = NOW(),
                aseguradora_aprobado = CASE WHEN @aprobado = 1 THEN 1 ELSE aseguradora_aprobado END,
                estado = CASE WHEN @aprobado = 1 OR aseguradora_aprobado = 1 THEN 'ASEGURADORA_APROBADO' ELSE 'EN_REVISION_ASEGURADORA' END,
                estado_reclamo = CASE WHEN @aprobado = 1 OR aseguradora_aprobado = 1 THEN 'ASEGURADORA_APROBADO' ELSE 'EN_REVISION_ASEGURADORA' END,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            respuesta = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim(),
            aprobado
        });
    }

    public async Task<IEnumerable<ReclamoRespuestaAseguradora>> GetRespuestasAseguradoraAsync(int reclamoId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                id Id,
                reclamo_id ReclamoId,
                origen Origen,
                remitente Remitente,
                asunto Asunto,
                respuesta Respuesta,
                aprobado Aprobado,
                requiere_rsa RequiereRsa,
                requiere_deducible RequiereDeducible,
                monto_rsa MontoRsa,
                monto_deducible MontoDeducible,
                solicita_mas_documentos SolicitaMasDocumentos,
                aprobado_sin_pagos_finales AprobadoSinPagosFinales,
                acciones Acciones,
                usuario_id UsuarioId,
                creado_en CreadoEn
            FROM reclamo_respuestas_aseguradora
            WHERE reclamo_id = @reclamoId
            ORDER BY creado_en DESC, id DESC;";

        return await cn.QueryAsync<ReclamoRespuestaAseguradora>(sql, new { reclamoId });
    }

    public async Task UpdateEstadoSeguimientoAsync(int id, string estadoSeguimiento, int? usuarioId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var estado = NormalizeEstadoSeguimiento(estadoSeguimiento);
        const string sql = @"
            UPDATE reclamos_whatsapp
            SET estado_seguimiento = @estado,
                fecha_ultima_revision = NOW(),
                usuario_ultima_revision_id = @usuarioId,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new { id, estado, usuarioId });
    }

    public async Task UpdateSeguimientoOperativoAsync(int id, ReclamoSeguimientoOperativoRequest request, int? usuarioId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var estadoDeducible = NormalizeEstadoPagoFinal(request.EstadoDeducible);
        var estadoRsa = NormalizeEstadoPagoFinal(request.EstadoRsa);
        var estadoCotizaciones = NormalizeEstadoCotizaciones(request.EstadoCotizaciones);
        const string sql = @"
            UPDATE reclamos_whatsapp
            SET monto_deducible = @montoDeducible,
                monto_rsa = @montoRsa,
                moneda_pagos_finales = 'LPS',
                estado_deducible = @estadoDeducible,
                estado_rsa = @estadoRsa,
                fecha_solicitud_deducible = CASE
                    WHEN @estadoDeducible IN ('PENDIENTE_MONTO','PENDIENTE_PAGO') THEN IFNULL(fecha_solicitud_deducible, NOW())
                    ELSE fecha_solicitud_deducible
                END,
                fecha_solicitud_rsa = CASE
                    WHEN @estadoRsa IN ('PENDIENTE_MONTO','PENDIENTE_PAGO') THEN IFNULL(fecha_solicitud_rsa, NOW())
                    ELSE fecha_solicitud_rsa
                END,
                estado_cotizaciones = @estadoCotizaciones,
                cotizaciones_nota = @cotizacionesNota,
                caso_especial = @casoEspecial,
                caso_especial_nota = @casoEspecialNota,
                fecha_ultima_revision = NOW(),
                usuario_ultima_revision_id = @usuarioId,
                actualizado_en = NOW()
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            montoDeducible = request.MontoDeducible,
            montoRsa = request.MontoRsa,
            estadoDeducible,
            estadoRsa,
            estadoCotizaciones,
            cotizacionesNota = string.IsNullOrWhiteSpace(request.CotizacionesNota) ? null : request.CotizacionesNota.Trim(),
            casoEspecial = request.CasoEspecial,
            casoEspecialNota = string.IsNullOrWhiteSpace(request.CasoEspecialNota) ? null : request.CasoEspecialNota.Trim(),
            usuarioId
        });
    }

    public async Task<ReclamoWhatsApp?> GetSiguientePendienteAsync(int? currentId = null)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                r.id Id, r.message_id MessageId, r.asunto Asunto, r.aseguradora Aseguradora, r.asegurado Asegurado,
                r.poliza Poliza, r.placa Placa, r.reclamo Reclamo, r.conductor Conductor,
                r.celular Celular, r.fecha_notificacion FechaNotificacion,
                r.lugar_accidente LugarAccidente, r.mensaje_whatsapp MensajeWhatsApp,
                r.cliente_id ClienteId, r.poliza_id PolizaId, r.numero_reclamo NumeroReclamo,
                r.fecha_reclamo FechaReclamo, r.tipo_reclamo TipoReclamo, r.estado_reclamo EstadoReclamo,
                r.taller_sugerido_id TallerSugeridoId, r.taller_asignado_id TallerAsignadoId,
                r.ciudad_detectada CiudadDetectada, r.motivo_sugerencia_taller MotivoSugerenciaTaller,
                r.descripcion Descripcion, r.monto_estimado MontoEstimado, r.monto_aprobado MontoAprobado, r.monto_pagado MontoPagado,
                r.correo_aseguradora_principal CorreoAseguradoraPrincipal, r.correo_aseguradora_copia CorreoAseguradoraCopia,
                r.respuesta_aseguradora RespuestaAseguradora, r.fecha_respuesta_aseguradora FechaRespuestaAseguradora,
                r.aseguradora_aprobado AseguradoraAprobado,
                r.monto_deducible MontoDeducible, r.monto_rsa MontoRsa, r.moneda_pagos_finales MonedaPagosFinales,
                r.estado_deducible EstadoDeducible, r.estado_rsa EstadoRsa,
                r.fecha_solicitud_deducible FechaSolicitudDeducible, r.fecha_solicitud_rsa FechaSolicitudRsa,
                r.estado_cotizaciones EstadoCotizaciones, r.cotizaciones_nota CotizacionesNota,
                r.caso_especial CasoEspecial, r.caso_especial_nota CasoEspecialNota,
                r.estado_seguimiento EstadoSeguimiento, r.fecha_ultima_revision FechaUltimaRevision,
                r.usuario_ultima_revision_id UsuarioUltimaRevisionId,
                r.actualizado_en ActualizadoEn,
                r.estado Estado, r.fecha_creacion FechaCreacion, r.fecha_envio FechaEnvio,
                r.error Error
            FROM reclamos_whatsapp r
            LEFT JOIN (
                SELECT reclamo_id, COUNT(1) pendientes
                FROM reclamo_documentos
                WHERE recibido = 0
                GROUP BY reclamo_id
            ) docs ON docs.reclamo_id = r.id
            WHERE COALESCE(r.estado_seguimiento, 'NO_REVISADO') <> 'LISTO'
              AND (r.estado NOT IN ('COMPLETO','CERRADO') OR COALESCE(docs.pendientes, 0) > 0)
              AND (@currentId IS NULL OR r.id > @currentId)
            ORDER BY
                CASE COALESCE(r.estado_seguimiento, 'NO_REVISADO')
                    WHEN 'NO_REVISADO' THEN 0
                    WHEN 'EN_REVISION' THEN 1
                    WHEN 'ESPERANDO_CLIENTE' THEN 2
                    WHEN 'ESPERANDO_ASEGURADORA' THEN 3
                    ELSE 4
                END,
                CASE WHEN COALESCE(docs.pendientes, 0) > 0 THEN 0 ELSE 1 END,
                r.id ASC
            LIMIT 1;";

        var next = await cn.QueryFirstOrDefaultAsync<ReclamoWhatsApp>(sql, new { currentId });
        if (next is not null || currentId is null)
            return next;

        return await cn.QueryFirstOrDefaultAsync<ReclamoWhatsApp>(sql, new { currentId = (int?)null });
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
                ADD COLUMN IF NOT EXISTS monto_deducible DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS monto_rsa DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS moneda_pagos_finales VARCHAR(10) NOT NULL DEFAULT 'LPS',
                ADD COLUMN IF NOT EXISTS estado_deducible VARCHAR(40) NOT NULL DEFAULT 'NO_APLICA',
                ADD COLUMN IF NOT EXISTS estado_rsa VARCHAR(40) NOT NULL DEFAULT 'NO_APLICA',
                ADD COLUMN IF NOT EXISTS fecha_solicitud_deducible DATETIME NULL,
                ADD COLUMN IF NOT EXISTS fecha_solicitud_rsa DATETIME NULL,
                ADD COLUMN IF NOT EXISTS estado_cotizaciones VARCHAR(50) NOT NULL DEFAULT 'PENDIENTE_VISITA_TALLERES',
                ADD COLUMN IF NOT EXISTS cotizaciones_nota TEXT NULL,
                ADD COLUMN IF NOT EXISTS caso_especial TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS caso_especial_nota TEXT NULL,
                ADD COLUMN IF NOT EXISTS estado_seguimiento VARCHAR(40) NOT NULL DEFAULT 'NO_REVISADO',
                ADD COLUMN IF NOT EXISTS fecha_ultima_revision DATETIME NULL,
                ADD COLUMN IF NOT EXISTS usuario_ultima_revision_id INT NULL,
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

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS reclamo_respuestas_aseguradora (
                id INT AUTO_INCREMENT PRIMARY KEY,
                reclamo_id INT NOT NULL,
                origen VARCHAR(40) NOT NULL DEFAULT 'MANUAL',
                remitente VARCHAR(180) NULL,
                asunto VARCHAR(255) NULL,
                respuesta TEXT NOT NULL,
                aprobado TINYINT(1) NOT NULL DEFAULT 0,
                requiere_rsa TINYINT(1) NOT NULL DEFAULT 0,
                requiere_deducible TINYINT(1) NOT NULL DEFAULT 0,
                monto_rsa DECIMAL(18,2) NULL,
                monto_deducible DECIMAL(18,2) NULL,
                solicita_mas_documentos TINYINT(1) NOT NULL DEFAULT 0,
                aprobado_sin_pagos_finales TINYINT(1) NOT NULL DEFAULT 0,
                acciones TEXT NULL,
                usuario_id INT NULL,
                creado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX ix_reclamo_respuestas_reclamo_fecha (reclamo_id, creado_en)
            );");

        await cn.ExecuteAsync(@"
            ALTER TABLE reclamo_respuestas_aseguradora
                ADD COLUMN IF NOT EXISTS monto_rsa DECIMAL(18,2) NULL AFTER requiere_deducible,
                ADD COLUMN IF NOT EXISTS monto_deducible DECIMAL(18,2) NULL AFTER monto_rsa;");

        await cn.ExecuteAsync(@"
            ALTER TABLE reclamo_documentos
                ADD COLUMN IF NOT EXISTS cantidad_requerida INT NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS minimo_aceptable INT NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS permite_excepcion TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS excepcion_aceptada TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS excepcion_observacion TEXT NULL;

            UPDATE reclamo_documentos
            SET cantidad_requerida = 2,
                minimo_aceptable = 1,
                permite_excepcion = 1
            WHERE LOWER(documento) LIKE '%cotizacion%'
               OR LOWER(documento) LIKE '%cotizaciones%';");
    }

    private static bool IsCotizacionDocumento(string? documento)
    {
        return !string.IsNullOrWhiteSpace(documento)
            && documento.Contains("cotizacion", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEstadoSeguimiento(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return normalized is "NO_REVISADO" or "EN_REVISION" or "ESPERANDO_CLIENTE" or "ESPERANDO_ASEGURADORA" or "LISTO"
            ? normalized
            : "EN_REVISION";
    }

    private static string NormalizeEstadoPagoFinal(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return normalized is "NO_APLICA" or "PENDIENTE_MONTO" or "PENDIENTE_PAGO" or "PAGADO_CLIENTE" or "COMPROBANTE_ENVIADO" or "CONFIRMADO_ASEGURADORA"
            ? normalized
            : "NO_APLICA";
    }

    private static string NormalizeEstadoCotizaciones(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return normalized is "PENDIENTE_VISITA_TALLERES" or "CLIENTE_INDICO_QUE_FUE" or "TALLER_INDICO_QUE_ENVIO" or "ASEGURADORA_CONFIRMADAS" or "NO_APLICA"
            ? normalized
            : "PENDIENTE_VISITA_TALLERES";
    }
}
