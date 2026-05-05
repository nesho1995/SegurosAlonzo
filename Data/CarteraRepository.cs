using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class CarteraRepository
{
    private readonly DbConnectionFactory _factory;
    private const string FinancialKeySql = @"
        CASE
            WHEN p.cliente_contratante_id IS NOT NULL THEN CONCAT(
                'FIN|', p.cliente_contratante_id, '|',
                UPPER(TRIM(COALESCE(p.aseguradora,''))), '|',
                UPPER(TRIM(COALESCE(p.numero_poliza,''))), '|',
                UPPER(TRIM(COALESCE(p.ramo,'')))
            )
            ELSE CONCAT('POL|', p.id)
        END";

    public CarteraRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureImportSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            ALTER TABLE clientes
                ADD COLUMN IF NOT EXISTS referido_por_nombre VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS referido_detectado TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS requiere_revision_manual TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS estado_revision VARCHAR(40) NOT NULL DEFAULT 'OK',
                ADD COLUMN IF NOT EXISTS motivo_revision TEXT NULL;

            ALTER TABLE polizas
                ADD COLUMN IF NOT EXISTS cliente_contratante_id INT NULL,
                ADD COLUMN IF NOT EXISTS vehiculo_id INT NULL,
                ADD COLUMN IF NOT EXISTS ramo_normalizado VARCHAR(60) NULL,
                ADD COLUMN IF NOT EXISTS extras_json TEXT NULL,
                ADD COLUMN IF NOT EXISTS numero_item VARCHAR(80) NULL,
                ADD COLUMN IF NOT EXISTS suma_asegurada_texto_original VARCHAR(255) NULL,
                ADD COLUMN IF NOT EXISTS maximo_vitalicio DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS suma_asegurada_vida DECIMAL(18,2) NULL,
                ADD COLUMN IF NOT EXISTS observacion2 TEXT NULL,
                ADD COLUMN IF NOT EXISTS tipo_proceso VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS estado_poliza_real VARCHAR(40) NULL,
                ADD COLUMN IF NOT EXISTS motivo_cancelacion TEXT NULL,
                ADD COLUMN IF NOT EXISTS motivo_estado_pago VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS origen_ramo_normalizado VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS origen_tipo_proceso VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS origen_estado_poliza_real VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS origen_estado_pago VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS origen_suma_asegurada VARCHAR(30) NULL,
                ADD COLUMN IF NOT EXISTS marca VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS modelo VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS anio INT NULL,
                ADD COLUMN IF NOT EXISTS color VARCHAR(80) NULL,
                ADD COLUMN IF NOT EXISTS tipo_vehiculo VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS placa VARCHAR(80) NULL,
                ADD COLUMN IF NOT EXISTS motor VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS vin_serie VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS chasis VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS agente_asignado VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS observacion_original TEXT NULL,
                ADD COLUMN IF NOT EXISTS observacion_tipo VARCHAR(40) NULL,
                ADD COLUMN IF NOT EXISTS persona_relacionada VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS nota_administrativa TEXT NULL,
                ADD COLUMN IF NOT EXISTS requiere_revision_manual TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS estado_revision VARCHAR(40) NOT NULL DEFAULT 'OK',
                ADD COLUMN IF NOT EXISTS motivo_revision TEXT NULL,
                ADD COLUMN IF NOT EXISTS fecha_actualizacion DATETIME NULL;

            ALTER TABLE clientes
                ADD COLUMN IF NOT EXISTS estado_negocio VARCHAR(30) NOT NULL DEFAULT 'ACTIVO';

            CREATE TABLE IF NOT EXISTS vehiculos (
                id INT AUTO_INCREMENT PRIMARY KEY,
                cliente_id INT NOT NULL,
                marca VARCHAR(120) NULL,
                modelo VARCHAR(120) NULL,
                anio INT NULL,
                color VARCHAR(80) NULL,
                tipo VARCHAR(120) NULL,
                placa VARCHAR(80) NULL,
                motor VARCHAR(120) NULL,
                vin_serie VARCHAR(120) NULL,
                chasis VARCHAR(120) NULL,
                origen_datos VARCHAR(40) NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                fecha_actualizacion DATETIME NULL,
                INDEX ix_vehiculos_cliente (cliente_id),
                INDEX ix_vehiculos_placa (placa),
                INDEX ix_vehiculos_vin (vin_serie),
                CONSTRAINT fk_vehiculos_cliente FOREIGN KEY (cliente_id) REFERENCES clientes(id) ON DELETE CASCADE
            );

            ALTER TABLE vehiculos
                ADD COLUMN IF NOT EXISTS fecha_actualizacion DATETIME NULL;

            CREATE TABLE IF NOT EXISTS vehiculo_historial (
                id INT AUTO_INCREMENT PRIMARY KEY,
                vehiculo_id INT NOT NULL,
                campo VARCHAR(40) NOT NULL,
                valor_anterior VARCHAR(180) NULL,
                valor_nuevo VARCHAR(180) NULL,
                origen VARCHAR(40) NULL,
                fecha DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX ix_vehiculo_historial_vehiculo (vehiculo_id),
                CONSTRAINT fk_vehiculo_historial_vehiculo FOREIGN KEY (vehiculo_id) REFERENCES vehiculos(id) ON DELETE CASCADE
            );");
    }

    public async Task<IEnumerable<Cliente>> GetClientesAsync()
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                id Id,
                nombre Nombre,
                telefono Telefono,
                telefono_secundario TelefonoSecundario,
                telefonos_extra_json TelefonosExtraJson,
                contacto Contacto,
                email Email,
                correos_extra_json CorreosExtraJson,
                identidad Identidad,
                fecha_nacimiento FechaNacimiento,
                ciudad Ciudad,
                observaciones Observaciones,
                referido_por_nombre ReferidoPorNombre,
                referido_detectado ReferidoDetectado,
                requiere_revision_manual RequiereRevisionManual,
                estado_revision EstadoRevision,
                motivo_revision MotivoRevision,
                notas_calidad_json NotasCalidadJson,
                datos_revisados DatosRevisados,
                fecha_revision FechaRevision,
                usuario_revision_id UsuarioRevisionId,
                activo Activo,
                fecha_creacion FechaCreacion
            FROM clientes
            ORDER BY nombre;";

        return await cn.QueryAsync<Cliente>(sql);
    }

    public async Task<(IEnumerable<ClienteListado> items, int total)> GetClientesListadoAsync(
        string? buscar = null,
        string? estado = "ACTIVO",
        int pagina = 1,
        int pageSize = 25,
        string? financiera = null,
        string? aseguradora = null,
        string? ramo = null,
        string? estadoPago = null,
        string? ciudad = null)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        pagina = pagina < 1 ? 1 : pagina;
        pageSize = pageSize is < 10 or > 100 ? 25 : pageSize;

        var where = new List<string>();
        var parameters = new DynamicParameters();

        // Excluir clientes que solo aparecen como contratante/financiera (ej. PRESTADITO)
        where.Add(@"NOT (
            NOT EXISTS (SELECT 1 FROM polizas px WHERE px.cliente_id = c.id)
            AND EXISTS (SELECT 1 FROM polizas px WHERE px.cliente_contratante_id = c.id)
        )");

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            where.Add(@"(
                c.nombre LIKE @buscar
                OR c.telefono LIKE @buscar
                OR c.contacto LIKE @buscar
                OR c.email LIKE @buscar
                OR c.ciudad LIKE @buscar
                OR EXISTS (
                    SELECT 1
                    FROM polizas px
                    WHERE px.cliente_id = c.id
                      AND (
                        px.numero_poliza LIKE @buscar
                        OR px.aseguradora LIKE @buscar
                        OR px.ramo LIKE @buscar
                      )
                )
            )");
            parameters.Add("buscar", $"%{buscar.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(financiera))
        {
            where.Add(@"EXISTS (
                    SELECT 1
                    FROM polizas pf
                    LEFT JOIN clientes cf ON cf.id = pf.cliente_contratante_id
                    WHERE pf.cliente_id = c.id
                      AND (cf.nombre LIKE @financiera OR pf.observaciones LIKE @financiera)
                )");
            parameters.Add("financiera", $"%{financiera.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(aseguradora))
        {
            where.Add(@"EXISTS (
                    SELECT 1
                    FROM polizas pa
                    WHERE pa.cliente_id = c.id
                      AND pa.aseguradora LIKE @aseguradora
                )");
            parameters.Add("aseguradora", $"%{aseguradora.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(ramo))
        {
            where.Add(@"EXISTS (
                    SELECT 1
                    FROM polizas pr
                    WHERE pr.cliente_id = c.id
                      AND (pr.ramo LIKE @ramo OR pr.ramo_normalizado LIKE @ramo)
                )");
            parameters.Add("ramo", $"%{ramo.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(estadoPago))
        {
            where.Add(@"EXISTS (
                    SELECT 1
                    FROM polizas pp
                    WHERE pp.cliente_id = c.id
                      AND pp.estado_pago = @estadoPago
                )");
            parameters.Add("estadoPago", estadoPago.Trim());
        }

        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            where.Add("c.ciudad LIKE @ciudad");
            parameters.Add("ciudad", $"%{ciudad.Trim()}%");
        }

        if (estado == "ACTIVO")
            where.Add("c.activo = 1");
        else if (estado == "INACTIVO")
            where.Add("c.activo = 0");
        else if (estado == "OK")
            where.Add(@"COALESCE(c.estado_revision, 'OK') = 'OK'
                        AND COALESCE(c.requiere_revision_manual, 0) = 0
                        AND NOT EXISTS (
                            SELECT 1 FROM polizas pz
                            WHERE pz.cliente_id = c.id
                              AND (COALESCE(pz.requiere_revision_manual,0) = 1 OR COALESCE(pz.estado_revision,'OK') <> 'OK')
                        )");
        else if (estado == "PENDIENTE_REVISION")
            where.Add(@"COALESCE(c.requiere_revision_manual,0) = 1
                        OR COALESCE(c.estado_revision,'OK') = 'PENDIENTE_REVISION'
                        OR EXISTS (
                            SELECT 1 FROM polizas pz
                            WHERE pz.cliente_id = c.id
                              AND (COALESCE(pz.requiere_revision_manual,0) = 1 OR COALESCE(pz.estado_revision,'OK') = 'PENDIENTE_REVISION')
                        )");
        else if (estado == "ERROR_IMPORTACION")
            where.Add(@"COALESCE(c.estado_revision,'OK') = 'ERROR_IMPORTACION'
                        OR EXISTS (
                            SELECT 1 FROM polizas pz
                            WHERE pz.cliente_id = c.id
                              AND COALESCE(pz.estado_revision,'OK') = 'ERROR_IMPORTACION'
                        )");

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        parameters.Add("offset", (pagina - 1) * pageSize);
        parameters.Add("pageSize", pageSize);

        var sql = $@"
            SELECT
                c.id Id,
                c.nombre Nombre,
                c.telefono Telefono,
                c.telefono_secundario TelefonoSecundario,
                c.telefonos_extra_json TelefonosExtraJson,
                c.contacto Contacto,
                c.email Email,
                c.correos_extra_json CorreosExtraJson,
                c.ciudad Ciudad,
                c.activo Activo,
                c.requiere_revision_manual RequiereRevisionManual,
                c.estado_revision EstadoRevision,
                c.motivo_revision MotivoRevision,
                c.fecha_creacion FechaCreacion,
                COALESCE(c.estado_negocio, 'ACTIVO') EstadoNegocio,
                COUNT(p.id) Polizas,
                COALESCE(SUM(CASE WHEN p.activo = 1 THEN 1 ELSE 0 END), 0) PolizasActivas
            FROM clientes c
            LEFT JOIN polizas p ON p.cliente_id = c.id
            {whereSql}
            GROUP BY c.id, c.nombre, c.telefono, c.telefono_secundario, c.telefonos_extra_json, c.contacto, c.email, c.correos_extra_json, c.ciudad, c.activo, c.requiere_revision_manual, c.estado_revision, c.motivo_revision, c.fecha_creacion, c.estado_negocio
            ORDER BY c.nombre
            LIMIT @pageSize OFFSET @offset;";

        var countSql = $@"
            SELECT COUNT(*)
            FROM clientes c
            {whereSql};";

        var items = await cn.QueryAsync<ClienteListado>(sql, parameters);
        var total = await cn.ExecuteScalarAsync<int>(countSql, parameters);

        return (items, total);
    }

    public async Task<Cliente?> GetClienteByIdAsync(int id)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                id Id,
                nombre Nombre,
                telefono Telefono,
                telefono_secundario TelefonoSecundario,
                telefonos_extra_json TelefonosExtraJson,
                contacto Contacto,
                email Email,
                correos_extra_json CorreosExtraJson,
                identidad Identidad,
                fecha_nacimiento FechaNacimiento,
                ciudad Ciudad,
                observaciones Observaciones,
                referido_por_nombre ReferidoPorNombre,
                referido_detectado ReferidoDetectado,
                requiere_revision_manual RequiereRevisionManual,
                estado_revision EstadoRevision,
                motivo_revision MotivoRevision,
                notas_calidad_json NotasCalidadJson,
                datos_revisados DatosRevisados,
                fecha_revision FechaRevision,
                usuario_revision_id UsuarioRevisionId,
                activo Activo,
                fecha_creacion FechaCreacion
            FROM clientes
            WHERE id = @id;";

        return await cn.QueryFirstOrDefaultAsync<Cliente>(sql, new { id });
    }

    public async Task<int> InsertClienteAsync(Cliente c)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            INSERT INTO clientes
            (
                nombre,
                telefono,
                telefono_secundario,
                telefonos_extra_json,
                contacto,
                email,
                correos_extra_json,
                identidad,
                fecha_nacimiento,
                ciudad,
                observaciones,
                referido_por_nombre,
                referido_detectado,
                requiere_revision_manual,
                estado_revision,
                motivo_revision,
                notas_calidad_json,
                datos_revisados,
                fecha_revision,
                usuario_revision_id,
                activo
            )
            VALUES
            (
                @Nombre,
                @Telefono,
                @TelefonoSecundario,
                @TelefonosExtraJson,
                @Contacto,
                @Email,
                @CorreosExtraJson,
                @Identidad,
                @FechaNacimiento,
                @Ciudad,
                @Observaciones,
                @ReferidoPorNombre,
                @ReferidoDetectado,
                @RequiereRevisionManual,
                @EstadoRevision,
                @MotivoRevision,
                @NotasCalidadJson,
                @DatosRevisados,
                @FechaRevision,
                @UsuarioRevisionId,
                @Activo
            );

            SELECT LAST_INSERT_ID();";

        return await cn.ExecuteScalarAsync<int>(sql, c);
    }

    public async Task UpdateClienteAsync(Cliente c)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE clientes
            SET nombre = @Nombre,
                telefono = @Telefono,
                telefono_secundario = @TelefonoSecundario,
                telefonos_extra_json = @TelefonosExtraJson,
                contacto = @Contacto,
                email = @Email,
                correos_extra_json = @CorreosExtraJson,
                identidad = @Identidad,
                fecha_nacimiento = @FechaNacimiento,
                ciudad = @Ciudad,
                observaciones = @Observaciones,
                referido_por_nombre = @ReferidoPorNombre,
                referido_detectado = @ReferidoDetectado,
                requiere_revision_manual = @RequiereRevisionManual,
                estado_revision = @EstadoRevision,
                motivo_revision = @MotivoRevision,
                notas_calidad_json = @NotasCalidadJson,
                datos_revisados = @DatosRevisados,
                fecha_revision = @FechaRevision,
                usuario_revision_id = @UsuarioRevisionId,
                activo = @Activo
            WHERE id = @Id;";

        await cn.ExecuteAsync(sql, c);
    }

    public async Task<int> GetOrCreateClienteAsync(string nombre)
    {
        return await GetOrCreateClienteAsync(nombre, null, null);
    }

    public async Task<int> GetOrCreateClienteAsync(string nombre, string? telefono, string? email)
    {
        using var cn = _factory.CreateConnection();

        nombre = nombre.Trim();

        const string buscarSql = @"
            SELECT id
            FROM clientes
            WHERE UPPER(TRIM(nombre)) = UPPER(TRIM(@nombre))
              AND (
                @telefono IS NULL
                OR @telefono = ''
                OR telefono = @telefono
                OR contacto = @telefono
                OR EXISTS (
                    SELECT 1
                    FROM cliente_telefonos t
                    WHERE t.cliente_id = clientes.id
                      AND t.telefono = @telefono
                )
              )
              AND (
                @email IS NULL
                OR @email = ''
                OR email = @email
              )
            LIMIT 1;";

        var existente = await cn.ExecuteScalarAsync<int?>(buscarSql, new { nombre, telefono, email });

        if (!existente.HasValue && (!string.IsNullOrWhiteSpace(telefono) || !string.IsNullOrWhiteSpace(email)))
        {
            existente = await cn.ExecuteScalarAsync<int?>(@"
                SELECT id
                FROM clientes
                WHERE UPPER(TRIM(nombre)) = UPPER(TRIM(@nombre))
                LIMIT 1;", new { nombre });
        }

        if (existente.HasValue)
            return existente.Value;

        const string insertSql = @"
            INSERT INTO clientes (nombre)
            VALUES (@nombre);

            SELECT LAST_INSERT_ID();";

        return await cn.ExecuteScalarAsync<int>(insertSql, new { nombre });
    }

    public async Task ActualizarDatosClienteAsync(int clienteId, Cliente c)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE clientes
            SET telefono = COALESCE(NULLIF(@Telefono,''), telefono),
                telefono_secundario = COALESCE(NULLIF(@TelefonoSecundario,''), telefono_secundario),
                telefonos_extra_json = COALESCE(NULLIF(@TelefonosExtraJson,''), telefonos_extra_json),
                contacto = COALESCE(NULLIF(@Contacto,''), contacto),
                email = COALESCE(NULLIF(@Email,''), email),
                correos_extra_json = COALESCE(NULLIF(@CorreosExtraJson,''), correos_extra_json),
                fecha_nacimiento = COALESCE(@FechaNacimiento, fecha_nacimiento),
                ciudad = COALESCE(NULLIF(@Ciudad,''), ciudad),
                observaciones = COALESCE(NULLIF(@Observaciones,''), observaciones),
                referido_por_nombre = COALESCE(NULLIF(@ReferidoPorNombre,''), referido_por_nombre),
                referido_detectado = CASE WHEN @ReferidoDetectado = 1 THEN 1 ELSE referido_detectado END,
                requiere_revision_manual = CASE WHEN @RequiereRevisionManual = 1 THEN 1 ELSE requiere_revision_manual END,
                estado_revision = CASE WHEN @EstadoRevision IS NULL OR @EstadoRevision = '' THEN estado_revision ELSE @EstadoRevision END,
                motivo_revision = CASE WHEN @MotivoRevision IS NULL OR @MotivoRevision = '' THEN motivo_revision ELSE @MotivoRevision END,
                notas_calidad_json = COALESCE(NULLIF(@NotasCalidadJson,''), notas_calidad_json)
            WHERE id = @clienteId;";

        await cn.ExecuteAsync(sql, new
        {
            clienteId,
            c.Telefono,
            c.TelefonoSecundario,
            c.TelefonosExtraJson,
            c.Contacto,
            c.Email,
            c.CorreosExtraJson,
            c.FechaNacimiento,
            c.Ciudad,
            c.Observaciones,
            c.ReferidoPorNombre,
            c.ReferidoDetectado,
            c.RequiereRevisionManual,
            c.EstadoRevision,
            c.MotivoRevision,
            c.NotasCalidadJson
        });
    }

    public async Task InsertTelefonoAsync(int clienteId, string telefono, bool principal)
    {
        using var cn = _factory.CreateConnection();

        if (string.IsNullOrWhiteSpace(telefono))
            return;

        const string existeSql = @"
            SELECT COUNT(1)
            FROM cliente_telefonos
            WHERE cliente_id = @clienteId
              AND telefono = @telefono;";

        var existe = await cn.ExecuteScalarAsync<int>(existeSql, new { clienteId, telefono });

        if (existe > 0)
            return;

        const string sql = @"
            INSERT INTO cliente_telefonos
            (cliente_id, telefono, tipo, principal, activo)
            VALUES
            (@clienteId, @telefono, 'GENERAL', @principal, 1);";

        await cn.ExecuteAsync(sql, new { clienteId, telefono, principal });
    }

    public async Task<IEnumerable<string>> GetTelefonosClienteAsync(int clienteId)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT telefono
            FROM cliente_telefonos
            WHERE cliente_id = @clienteId
              AND activo = 1
            ORDER BY principal DESC, id ASC;";

        return await cn.QueryAsync<string>(sql, new { clienteId });
    }

    public async Task<int> InsertPolizaAsync(Poliza p)
    {
        var (id, _) = await InsertPolizaInternalAsync(p);
        return id;
    }

    public async Task<bool> InsertPolizaIfNotExistsAsync(Poliza p)
    {
        var (_, inserted) = await InsertPolizaInternalAsync(p);
        return inserted;
    }

    public async Task<(int id, bool inserted)> InsertPolizaIfNotExistsDetailedAsync(Poliza p)
    {
        return await InsertPolizaInternalAsync(p);
    }

    private async Task<(int id, bool inserted)> InsertPolizaInternalAsync(Poliza p)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        var existente = await cn.ExecuteScalarAsync<int?>(@"
            SELECT id
            FROM polizas
            WHERE IFNULL(numero_poliza,'') = IFNULL(@NumeroPoliza,'')
              AND IFNULL(numero_item,'') = IFNULL(NULLIF(@NumeroItem,''),'')
              AND cliente_id = @ClienteId
              AND IFNULL(certificado,'') = IFNULL(@Certificado,'')
              AND IFNULL(endoso,'') = IFNULL(@Endoso,'')
            LIMIT 1;", p);

        if (existente.HasValue)
        {
            await cn.ExecuteAsync(@"
                UPDATE polizas
                SET cliente_id = @ClienteId,
                    cliente_contratante_id = COALESCE(@ClienteContratanteId, cliente_contratante_id),
                    vehiculo_id = COALESCE(@VehiculoId, vehiculo_id)
                WHERE id = @Id;", new
            {
                Id = existente.Value,
                p.ClienteId,
                p.ClienteContratanteId,
                p.VehiculoId
            });
            return (existente.Value, false);
        }

        const string sql = @"
            INSERT INTO polizas
            (
                cliente_id,
                cliente_contratante_id,
                vehiculo_id,
                aseguradora,
                ramo,
                ramo_normalizado,
                extras_json,
                cuotas,
                forma_pago,
                numero_poliza,
                numero_item,
                certificado,
                endoso,
                prima_neta,
                seguro_asiento,
                prima_comercial,
                impuesto,
                gastos_emision,
                bomberos,
                prima_total,
                plan,
                suma_asegurada,
                suma_asegurada_texto_original,
                maximo_vitalicio,
                suma_asegurada_vida,
                mes_inicio_poliza,
                vigencia,
                hasta,
                medio,
                vehiculo,
                marca,
                modelo,
                anio,
                color,
                tipo_vehiculo,
                placa,
                motor,
                vin_serie,
                chasis,
                agente_asignado,
                emision_renovacion,
                observacion2,
                tipo_proceso,
                estado_poliza_real,
                motivo_cancelacion,
                motivo_estado_pago,
                origen_ramo_normalizado,
                origen_tipo_proceso,
                origen_estado_poliza_real,
                origen_estado_pago,
                origen_suma_asegurada,
                estado_pago,
                observaciones,
                observacion_original,
                observacion_tipo,
                persona_relacionada,
                nota_administrativa,
                requiere_revision_manual,
                estado_revision,
                motivo_revision,
                activo
            )
            VALUES
            (
                @ClienteId,
                @ClienteContratanteId,
                @VehiculoId,
                @Aseguradora,
                @Ramo,
                @RamoNormalizado,
                @ExtrasJson,
                @Cuotas,
                @FormaPago,
                @NumeroPoliza,
                NULLIF(@NumeroItem,''),
                @Certificado,
                @Endoso,
                @PrimaNeta,
                @SeguroAsiento,
                @PrimaComercial,
                @Impuesto,
                @GastosEmision,
                @Bomberos,
                @PrimaTotal,
                @Plan,
                @SumaAsegurada,
                @SumaAseguradaTextoOriginal,
                @MaximoVitalicio,
                @SumaAseguradaVida,
                @MesInicioPoliza,
                @Vigencia,
                @Hasta,
                @Medio,
                @Vehiculo,
                @Marca,
                @Modelo,
                @Anio,
                @Color,
                @TipoVehiculo,
                @Placa,
                @Motor,
                @VinSerie,
                @Chasis,
                @AgenteAsignado,
                @EmisionRenovacion,
                @Observacion2,
                @TipoProceso,
                @EstadoPolizaReal,
                @MotivoCancelacion,
                @MotivoEstadoPago,
                @OrigenRamoNormalizado,
                @OrigenTipoProceso,
                @OrigenEstadoPolizaReal,
                @OrigenEstadoPago,
                @OrigenSumaAsegurada,
                @EstadoPago,
                @Observaciones,
                @ObservacionOriginal,
                @ObservacionTipo,
                @PersonaRelacionada,
                @NotaAdministrativa,
                @RequiereRevisionManual,
                @EstadoRevision,
                @MotivoRevision,
                @Activo
            );
            SELECT LAST_INSERT_ID();";

        var id = await cn.ExecuteScalarAsync<int>(sql, p);
        return (id, true);
    }

    public async Task<IEnumerable<Poliza>> GetPolizasByClienteAsync(int clienteId)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                p.id Id,
                p.cliente_id ClienteId,
                p.cliente_contratante_id ClienteContratanteId,
                cc.nombre ClienteContratanteNombre,
                vehiculo_id VehiculoId,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                p.ramo_normalizado RamoNormalizado,
                p.extras_json ExtrasJson,
                p.cuotas Cuotas,
                p.forma_pago FormaPago,
                p.numero_poliza NumeroPoliza,
                p.numero_item NumeroItem,
                p.certificado Certificado,
                p.endoso Endoso,
                p.prima_neta PrimaNeta,
                p.seguro_asiento SeguroAsiento,
                p.prima_comercial PrimaComercial,
                p.impuesto Impuesto,
                p.gastos_emision GastosEmision,
                p.bomberos Bomberos,
                p.prima_total PrimaTotal,
                p.plan Plan,
                p.suma_asegurada SumaAsegurada,
                p.suma_asegurada_texto_original SumaAseguradaTextoOriginal,
                p.maximo_vitalicio MaximoVitalicio,
                p.suma_asegurada_vida SumaAseguradaVida,
                p.mes_inicio_poliza MesInicioPoliza,
                p.vigencia Vigencia,
                p.hasta Hasta,
                p.medio Medio,
                p.vehiculo Vehiculo,
                p.marca Marca,
                p.modelo Modelo,
                p.anio Anio,
                p.color Color,
                p.tipo_vehiculo TipoVehiculo,
                p.placa Placa,
                p.motor Motor,
                p.vin_serie VinSerie,
                p.chasis Chasis,
                p.agente_asignado AgenteAsignado,
                p.emision_renovacion EmisionRenovacion,
                p.observacion2 Observacion2,
                p.tipo_proceso TipoProceso,
                p.estado_poliza_real EstadoPolizaReal,
                p.motivo_cancelacion MotivoCancelacion,
                p.motivo_estado_pago MotivoEstadoPago,
                p.origen_ramo_normalizado OrigenRamoNormalizado,
                p.origen_tipo_proceso OrigenTipoProceso,
                p.origen_estado_poliza_real OrigenEstadoPolizaReal,
                p.origen_estado_pago OrigenEstadoPago,
                p.origen_suma_asegurada OrigenSumaAsegurada,
                p.estado_pago EstadoPago,
                p.observaciones Observaciones,
                p.observacion_original ObservacionOriginal,
                p.observacion_tipo ObservacionTipo,
                p.persona_relacionada PersonaRelacionada,
                p.nota_administrativa NotaAdministrativa,
                p.requiere_revision_manual RequiereRevisionManual,
                p.estado_revision EstadoRevision,
                p.motivo_revision MotivoRevision,
                p.notas_calidad_json NotasCalidadJson,
                p.datos_revisados DatosRevisados,
                p.fecha_revision FechaRevision,
                p.usuario_revision_id UsuarioRevisionId,
                p.activo Activo,
                p.fecha_inicio FechaInicio,
                p.fecha_fin FechaFin
            FROM polizas p
            LEFT JOIN clientes cc ON cc.id = p.cliente_contratante_id
            WHERE p.cliente_id = @clienteId
            ORDER BY p.id DESC;";

        return await cn.QueryAsync<Poliza>(sql, new { clienteId });
    }

    public async Task<IEnumerable<Cliente>> GetAllAsync()
    {
        return await GetClientesAsync();
    }

    public async Task<dynamic> GetDashboardStatsAsync(string? aseguradora = null, string? ciudad = null, DateTime? desde = null, DateTime? hasta = null)
    {
        using var cn = _factory.CreateConnection();

        var polizaWhere = BuildDashboardWhere("p", aseguradora, ciudad, desde, hasta, out var parameters);

        // Pólizas compartidas: misma numero_poliza para varios clientes debe contarse UNA sola vez.
        // Agrupa por IFNULL(NULLIF(numero_poliza,''), id) y toma MIN(id) como representante.
        string Dedup(string extraWhere = "")
        {
            var w = string.IsNullOrEmpty(extraWhere) ? polizaWhere : AppendWhere(polizaWhere, extraWhere);
            return $"SELECT MIN(p.id) id FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {w} GROUP BY IFNULL(NULLIF(p.numero_poliza,''), p.id)";
        }

        var sql = $@"
            SELECT
                (SELECT COUNT(DISTINCT c.id) FROM clientes c WHERE (@ciudad IS NULL OR c.ciudad LIKE @ciudadLike)) TotalClientes,
                (SELECT COUNT(DISTINCT c.id) FROM clientes c WHERE c.activo = 1 AND (@ciudad IS NULL OR c.ciudad LIKE @ciudadLike)) ClientesActivos,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {polizaWhere}) TotalPolizas,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1")}) PolizasActivas,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1 AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)")}) PolizasPorVencer30,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1 AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 15 DAY)")}) PolizasPorVencer15,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1 AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 7 DAY)")}) PolizasPorVencer7,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1 AND p.hasta < CURDATE()")}) PolizasVencidas,
                (SELECT COUNT(DISTINCT {FinancialKeySql}) FROM polizas p INNER JOIN clientes c ON c.id = p.cliente_id {AppendWhere(polizaWhere, "p.activo = 1 AND p.estado_pago IN ('SIN_VALIDAR','PENDIENTE','MORA')")}) PagosPendientes,
                (SELECT COALESCE(SUM(x.prima_total), 0)
                 FROM (
                    SELECT {FinancialKeySql} financial_key, MAX(p.prima_total) prima_total
                    FROM polizas p
                    INNER JOIN clientes c ON c.id = p.cliente_id
                    {AppendWhere(polizaWhere, "p.activo = 1")}
                    GROUP BY {FinancialKeySql}
                 ) x) PrimaTotalActiva;";

        return await cn.QueryFirstAsync(sql, parameters);
    }

    public async Task<IEnumerable<PolizaResumenDashboard>> GetProximasRenovacionesAsync(int dias = 45, int limit = 8, string? aseguradora = null, string? ciudad = null)
    {
        using var cn = _factory.CreateConnection();

        // GROUP BY numero_poliza para evitar duplicar pólizas compartidas entre varios clientes
        const string sql = @"
            SELECT
                MIN(p.id) Id,
                MIN(p.cliente_id) ClienteId,
                MIN(c.nombre) Cliente,
                MIN(p.aseguradora) Aseguradora,
                p.numero_poliza NumeroPoliza,
                MIN(p.ramo) Ramo,
                MIN(p.hasta) Hasta,
                MIN(p.prima_total) PrimaTotal,
                DATEDIFF(MIN(p.hasta), CURDATE()) DiasRestantes,
                MIN(p.estado_pago) EstadoPago
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE p.activo = 1
              AND p.hasta IS NOT NULL
              AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL @dias DAY)
              AND (@aseguradora IS NULL OR p.aseguradora LIKE @aseguradoraLike)
              AND (@ciudad IS NULL OR c.ciudad LIKE @ciudadLike)
            GROUP BY IFNULL(NULLIF(p.numero_poliza,''), p.id)
            ORDER BY MIN(p.hasta) ASC
            LIMIT @limit;";

        return await cn.QueryAsync<PolizaResumenDashboard>(sql, new
        {
            dias,
            limit,
            aseguradora = string.IsNullOrWhiteSpace(aseguradora) ? null : aseguradora.Trim(),
            aseguradoraLike = $"%{aseguradora?.Trim()}%",
            ciudad = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim(),
            ciudadLike = $"%{ciudad?.Trim()}%"
        });
    }

    public async Task<IEnumerable<string>> GetAseguradorasAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<string>(@"
            SELECT DISTINCT aseguradora
            FROM polizas
            WHERE aseguradora IS NOT NULL AND aseguradora <> ''
            ORDER BY aseguradora
            LIMIT 100;");
    }

    public async Task<IEnumerable<string>> GetCiudadesAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<string>(@"
            SELECT DISTINCT ciudad
            FROM clientes
            WHERE ciudad IS NOT NULL AND ciudad <> ''
            ORDER BY ciudad
            LIMIT 100;");
    }

    private static string BuildDashboardWhere(string alias, string? aseguradora, string? ciudad, DateTime? desde, DateTime? hasta, out DynamicParameters parameters)
    {
        parameters = new DynamicParameters();
        var where = new List<string>();
        parameters.Add("aseguradora", string.IsNullOrWhiteSpace(aseguradora) ? null : aseguradora.Trim());
        parameters.Add("aseguradoraLike", $"%{aseguradora?.Trim()}%");
        parameters.Add("ciudad", string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim());
        parameters.Add("ciudadLike", $"%{ciudad?.Trim()}%");
        parameters.Add("desde", desde?.Date);
        parameters.Add("hasta", hasta?.Date);

        if (!string.IsNullOrWhiteSpace(aseguradora))
            where.Add($"{alias}.aseguradora LIKE @aseguradoraLike");
        if (!string.IsNullOrWhiteSpace(ciudad))
            where.Add("c.ciudad LIKE @ciudadLike");
        if (desde.HasValue)
            where.Add($"{alias}.hasta >= @desde");
        if (hasta.HasValue)
            where.Add($"{alias}.hasta <= @hasta");

        return where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
    }

    private static string AppendWhere(string whereSql, string condition)
    {
        return string.IsNullOrWhiteSpace(whereSql)
            ? "WHERE " + condition
            : whereSql + " AND " + condition;
    }

    public async Task<Poliza?> GetPolizaByIdAsync(int id)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT 
                p.id Id,
                p.cliente_id ClienteId,
                p.cliente_contratante_id ClienteContratanteId,
                cc.nombre ClienteContratanteNombre,
                p.vehiculo_id VehiculoId,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                p.ramo_normalizado RamoNormalizado,
                p.extras_json ExtrasJson,
                p.cuotas Cuotas,
                p.forma_pago FormaPago,
                p.numero_poliza NumeroPoliza,
                p.numero_item NumeroItem,
                p.certificado Certificado,
                p.endoso Endoso,
                p.prima_neta PrimaNeta,
                p.seguro_asiento SeguroAsiento,
                p.prima_comercial PrimaComercial,
                p.impuesto Impuesto,
                p.gastos_emision GastosEmision,
                p.bomberos Bomberos,
                p.prima_total PrimaTotal,
                p.plan Plan,
                p.suma_asegurada SumaAsegurada,
                p.suma_asegurada_texto_original SumaAseguradaTextoOriginal,
                p.maximo_vitalicio MaximoVitalicio,
                p.suma_asegurada_vida SumaAseguradaVida,
                p.mes_inicio_poliza MesInicioPoliza,
                p.vigencia Vigencia,
                p.hasta Hasta,
                p.medio Medio,
                p.vehiculo Vehiculo,
                p.marca Marca,
                p.modelo Modelo,
                p.anio Anio,
                p.color Color,
                p.tipo_vehiculo TipoVehiculo,
                p.placa Placa,
                p.motor Motor,
                p.vin_serie VinSerie,
                p.chasis Chasis,
                p.agente_asignado AgenteAsignado,
                p.emision_renovacion EmisionRenovacion,
                p.observacion2 Observacion2,
                p.tipo_proceso TipoProceso,
                p.estado_poliza_real EstadoPolizaReal,
                p.motivo_cancelacion MotivoCancelacion,
                p.motivo_estado_pago MotivoEstadoPago,
                p.origen_ramo_normalizado OrigenRamoNormalizado,
                p.origen_tipo_proceso OrigenTipoProceso,
                p.origen_estado_poliza_real OrigenEstadoPolizaReal,
                p.origen_estado_pago OrigenEstadoPago,
                p.origen_suma_asegurada OrigenSumaAsegurada,
                p.estado_pago EstadoPago,
                p.observaciones Observaciones,
                p.observacion_original ObservacionOriginal,
                p.observacion_tipo ObservacionTipo,
                p.persona_relacionada PersonaRelacionada,
                p.nota_administrativa NotaAdministrativa,
                p.requiere_revision_manual RequiereRevisionManual,
                p.estado_revision EstadoRevision,
                p.motivo_revision MotivoRevision,
                p.notas_calidad_json NotasCalidadJson,
                p.datos_revisados DatosRevisados,
                p.fecha_revision FechaRevision,
                p.usuario_revision_id UsuarioRevisionId,
                p.activo Activo,
                p.fecha_inicio FechaInicio,
                p.fecha_fin FechaFin
            FROM polizas p
            LEFT JOIN clientes cc ON cc.id = p.cliente_contratante_id
            WHERE p.id = @id;";

        return await cn.QueryFirstOrDefaultAsync<Poliza>(sql, new { id });
    }

    public async Task UpdatePolizaAsync(Poliza p)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE polizas
            SET cliente_contratante_id = @ClienteContratanteId,
                vehiculo_id = @VehiculoId,
                aseguradora = @Aseguradora,
                ramo = @Ramo,
                ramo_normalizado = @RamoNormalizado,
                extras_json = @ExtrasJson,
                cuotas = @Cuotas,
                forma_pago = @FormaPago,
                numero_poliza = @NumeroPoliza,
                numero_item = NULLIF(@NumeroItem,''),
                certificado = @Certificado,
                endoso = @Endoso,
                prima_neta = @PrimaNeta,
                seguro_asiento = @SeguroAsiento,
                prima_comercial = @PrimaComercial,
                impuesto = @Impuesto,
                gastos_emision = @GastosEmision,
                bomberos = @Bomberos,
                prima_total = @PrimaTotal,
                plan = @Plan,
                suma_asegurada = @SumaAsegurada,
                suma_asegurada_texto_original = @SumaAseguradaTextoOriginal,
                maximo_vitalicio = @MaximoVitalicio,
                suma_asegurada_vida = @SumaAseguradaVida,
                mes_inicio_poliza = @MesInicioPoliza,
                vigencia = @Vigencia,
                hasta = @Hasta,
                medio = @Medio,
                vehiculo = @Vehiculo,
                marca = @Marca,
                modelo = @Modelo,
                anio = @Anio,
                color = @Color,
                tipo_vehiculo = @TipoVehiculo,
                placa = @Placa,
                motor = @Motor,
                vin_serie = @VinSerie,
                chasis = @Chasis,
                agente_asignado = @AgenteAsignado,
                emision_renovacion = @EmisionRenovacion,
                observacion2 = @Observacion2,
                tipo_proceso = @TipoProceso,
                estado_poliza_real = @EstadoPolizaReal,
                motivo_cancelacion = @MotivoCancelacion,
                motivo_estado_pago = @MotivoEstadoPago,
                origen_ramo_normalizado = @OrigenRamoNormalizado,
                origen_tipo_proceso = @OrigenTipoProceso,
                origen_estado_poliza_real = @OrigenEstadoPolizaReal,
                origen_estado_pago = @OrigenEstadoPago,
                origen_suma_asegurada = @OrigenSumaAsegurada,
                estado_pago = @EstadoPago,
                observaciones = @Observaciones,
                observacion_original = @ObservacionOriginal,
                observacion_tipo = @ObservacionTipo,
                persona_relacionada = @PersonaRelacionada,
                nota_administrativa = @NotaAdministrativa,
                requiere_revision_manual = @RequiereRevisionManual,
                estado_revision = @EstadoRevision,
                motivo_revision = @MotivoRevision,
                notas_calidad_json = @NotasCalidadJson,
                datos_revisados = @DatosRevisados,
                fecha_revision = @FechaRevision,
                usuario_revision_id = @UsuarioRevisionId,
                activo = @Activo,
                fecha_inicio = @FechaInicio,
                fecha_fin = @FechaFin
            WHERE id = @Id;";

        await cn.ExecuteAsync(sql, p);
        if (p.ClienteContratanteId.HasValue)
            await SyncFinancialGroupAsync(p.Id);
    }

    public async Task<int> GetFinancialMasterPolizaIdAsync(int polizaId)
    {
        using var cn = _factory.CreateConnection();
        var master = await cn.ExecuteScalarAsync<int?>(@"
            SELECT MIN(p2.id)
            FROM polizas p
            INNER JOIN polizas p2 ON p2.cliente_contratante_id = p.cliente_contratante_id
                AND UPPER(TRIM(COALESCE(p2.aseguradora,''))) = UPPER(TRIM(COALESCE(p.aseguradora,'')))
                AND UPPER(TRIM(COALESCE(p2.numero_poliza,''))) = UPPER(TRIM(COALESCE(p.numero_poliza,'')))
                AND UPPER(TRIM(COALESCE(p2.ramo,''))) = UPPER(TRIM(COALESCE(p.ramo,'')))
            WHERE p.id = @polizaId
              AND p.cliente_contratante_id IS NOT NULL;", new { polizaId });

        return master ?? polizaId;
    }

    public async Task SyncFinancialGroupAsync(int sourcePolizaId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE polizas target
            INNER JOIN polizas source ON source.id = @sourcePolizaId
                AND source.cliente_contratante_id IS NOT NULL
                AND target.cliente_contratante_id = source.cliente_contratante_id
                AND UPPER(TRIM(COALESCE(target.aseguradora,''))) = UPPER(TRIM(COALESCE(source.aseguradora,'')))
                AND UPPER(TRIM(COALESCE(target.numero_poliza,''))) = UPPER(TRIM(COALESCE(source.numero_poliza,'')))
                AND UPPER(TRIM(COALESCE(target.ramo,''))) = UPPER(TRIM(COALESCE(source.ramo,'')))
            SET target.prima_neta = source.prima_neta,
                target.seguro_asiento = source.seguro_asiento,
                target.prima_comercial = source.prima_comercial,
                target.impuesto = source.impuesto,
                target.gastos_emision = source.gastos_emision,
                target.bomberos = source.bomberos,
                target.prima_total = source.prima_total,
                target.cuotas = source.cuotas,
                target.forma_pago = source.forma_pago;", new { sourcePolizaId });
    }

    public async Task SetPolizaActivaAsync(int id, bool activo)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE polizas
            SET activo = @activo
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new { id, activo });
    }

    public async Task<int> CountDatosPendientesRevisionAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            SELECT
                (SELECT COUNT(*) FROM clientes WHERE datos_revisados = 0 AND (notas_calidad_json IS NOT NULL OR telefonos_extra_json IS NOT NULL OR correos_extra_json IS NOT NULL))
                +
                (SELECT COUNT(*) FROM polizas WHERE datos_revisados = 0 AND notas_calidad_json IS NOT NULL);");
    }

    public async Task MarcarClienteRevisadoAsync(int clienteId, int? usuarioRevisionId = null)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE clientes
            SET requiere_revision_manual = 0,
                estado_revision = 'OK',
                motivo_revision = NULL,
                datos_revisados = 1,
                fecha_revision = NOW(),
                usuario_revision_id = @usuarioRevisionId
            WHERE id = @clienteId;

            UPDATE polizas
            SET requiere_revision_manual = 0,
                estado_revision = 'OK',
                motivo_revision = NULL,
                datos_revisados = 1,
                fecha_revision = NOW(),
                usuario_revision_id = @usuarioRevisionId
            WHERE cliente_id = @clienteId;", new { clienteId, usuarioRevisionId });
    }

    public async Task<int?> UpsertVehiculoAsync(Vehiculo vehiculo)
    {
        await EnsureImportSchemaAsync();
        if (string.IsNullOrWhiteSpace(vehiculo.Marca)
            && string.IsNullOrWhiteSpace(vehiculo.Modelo)
            && vehiculo.Anio is null
            && string.IsNullOrWhiteSpace(vehiculo.Color)
            && string.IsNullOrWhiteSpace(vehiculo.Tipo)
            && string.IsNullOrWhiteSpace(vehiculo.Placa)
            && string.IsNullOrWhiteSpace(vehiculo.Motor)
            && string.IsNullOrWhiteSpace(vehiculo.VinSerie)
            && string.IsNullOrWhiteSpace(vehiculo.Chasis))
            return null;

        using var cn = _factory.CreateConnection();
        if (cn.State != System.Data.ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var existente = await cn.QueryFirstOrDefaultAsync<Vehiculo>(@"
            SELECT id Id, cliente_id ClienteId, marca Marca, modelo Modelo, anio Anio, color Color, tipo Tipo,
                   placa Placa, motor Motor, vin_serie VinSerie, chasis Chasis, origen_datos OrigenDatos,
                   activo Activo, fecha_creacion FechaCreacion, fecha_actualizacion FechaActualizacion
            FROM vehiculos
            WHERE (NULLIF(@VinSerie,'') IS NOT NULL AND vin_serie = @VinSerie)
               OR (NULLIF(@Placa,'') IS NOT NULL AND placa = @Placa)
            ORDER BY CASE WHEN vin_serie = @VinSerie THEN 0 ELSE 1 END
            LIMIT 1;", vehiculo, tx);

        if (existente is null)
        {
            var id = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO vehiculos
                (cliente_id, marca, modelo, anio, color, tipo, placa, motor, vin_serie, chasis, origen_datos, activo)
                VALUES
                (@ClienteId, @Marca, @Modelo, @Anio, @Color, @Tipo, @Placa, @Motor, @VinSerie, @Chasis, @OrigenDatos, 1);
                SELECT LAST_INSERT_ID();", vehiculo, tx);
            tx.Commit();
            return id;
        }

        await InsertVehicleHistoryIfChangedAsync(cn, tx, existente.Id, "placa", existente.Placa, vehiculo.Placa, vehiculo.OrigenDatos);
        await InsertVehicleHistoryIfChangedAsync(cn, tx, existente.Id, "vin_serie", existente.VinSerie, vehiculo.VinSerie, vehiculo.OrigenDatos);

        await cn.ExecuteAsync(@"
            UPDATE vehiculos
            SET cliente_id = @ClienteId,
                marca = COALESCE(NULLIF(@Marca,''), marca),
                modelo = COALESCE(NULLIF(@Modelo,''), modelo),
                anio = COALESCE(@Anio, anio),
                color = COALESCE(NULLIF(@Color,''), color),
                tipo = COALESCE(NULLIF(@Tipo,''), tipo),
                placa = COALESCE(NULLIF(@Placa,''), placa),
                motor = COALESCE(NULLIF(@Motor,''), motor),
                vin_serie = COALESCE(NULLIF(@VinSerie,''), vin_serie),
                chasis = COALESCE(NULLIF(@Chasis,''), chasis),
                origen_datos = COALESCE(NULLIF(@OrigenDatos,''), origen_datos),
                fecha_actualizacion = NOW()
            WHERE id = @Id;", new
        {
            existente.Id,
            vehiculo.ClienteId,
            vehiculo.Marca,
            vehiculo.Modelo,
            vehiculo.Anio,
            vehiculo.Color,
            vehiculo.Tipo,
            vehiculo.Placa,
            vehiculo.Motor,
            vehiculo.VinSerie,
            vehiculo.Chasis,
            vehiculo.OrigenDatos
        }, tx);

        tx.Commit();
        return existente.Id;
    }

    public async Task UpsertCuotasAsync(int polizaId, int cuotas, DateTime fechaInicio, IReadOnlyList<decimal?> montos)
    {
        cuotas = Math.Clamp(cuotas, 0, 12);
        if (cuotas == 0)
            return;

        using var cn = _factory.CreateConnection();
        if (cn.State != System.Data.ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        // Si todos los montos son 0 o nulos, calcular desde prima_total de la poliza
        var montosEfectivos = montos.Take(cuotas).Select(m => m ?? 0m).ToList();
        if (montosEfectivos.All(m => m == 0m))
        {
            var primaTotal = await cn.ExecuteScalarAsync<decimal?>(
                "SELECT prima_total FROM polizas WHERE id = @polizaId;",
                new { polizaId }, tx);
            if (primaTotal is > 0)
            {
                var montoBase = Math.Round(primaTotal.Value / cuotas, 2, MidpointRounding.AwayFromZero);
                for (var k = 0; k < cuotas; k++)
                    montosEfectivos[k] = k == cuotas - 1
                        ? Math.Round(primaTotal.Value - montoBase * (cuotas - 1), 2, MidpointRounding.AwayFromZero)
                        : montoBase;
            }
        }

        for (var i = 1; i <= cuotas; i++)
        {
            await cn.ExecuteAsync(@"
                INSERT INTO poliza_cuotas (poliza_id, numero_cuota, fecha_vencimiento, monto, estado)
                VALUES (@polizaId, @numeroCuota, @fechaVencimiento, @monto, 'PENDIENTE')
                ON DUPLICATE KEY UPDATE
                    fecha_vencimiento = VALUES(fecha_vencimiento),
                    monto = VALUES(monto),
                    estado = CASE
                        WHEN estado = 'PAGADA' THEN estado
                        WHEN estado = 'PARCIAL' THEN estado
                        ELSE VALUES(estado)
                    END;", new
            {
                polizaId,
                numeroCuota = i,
                fechaVencimiento = fechaInicio.AddMonths(i - 1),
                monto = montosEfectivos[i - 1]
            }, tx);
        }

        await cn.ExecuteAsync(@"
            DELETE FROM poliza_cuotas
            WHERE poliza_id = @polizaId
              AND numero_cuota > @cuotas
              AND estado NOT IN ('PAGADA','PARCIAL');", new { polizaId, cuotas }, tx);

        await RecalcularEstadoPolizaAsync(polizaId, cn, tx);
        tx.Commit();
    }

    public async Task<IEnumerable<PolizaCuota>> GetCuotasByPolizaAsync(int polizaId)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<PolizaCuota>(@"
            SELECT id Id, poliza_id PolizaId, numero_cuota NumeroCuota, fecha_vencimiento FechaVencimiento,
                   monto Monto, estado Estado, fecha_pago FechaPago, comprobante_url ComprobanteUrl,
                   documento_id DocumentoId, numero_recibo NumeroRecibo, referencia_banco ReferenciaBanco,
                   observaciones Observaciones, fecha_creacion FechaCreacion
            FROM poliza_cuotas
            WHERE poliza_id = @polizaId
            ORDER BY numero_cuota;", new { polizaId });
    }

    public async Task<bool> UpdateCuotaMontoAsync(int cuotaId, decimal monto)
    {
        if (monto < 0m)
            monto = 0m;

        using var cn = _factory.CreateConnection();
        if (cn.State != System.Data.ConnectionState.Open)
            cn.Open();
        using var tx = cn.BeginTransaction();

        var cuotaInfo = await cn.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT poliza_id PolizaId, numero_cuota NumeroCuota
            FROM poliza_cuotas
            WHERE id = @cuotaId;", new { cuotaId }, tx);
        if (cuotaInfo is null)
        {
            tx.Rollback();
            return false;
        }

        var polizaId = Convert.ToInt32(cuotaInfo.PolizaId);
        var numeroCuota = Convert.ToInt32(cuotaInfo.NumeroCuota);

        await cn.ExecuteAsync(@"
            UPDATE poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN polizas source ON source.id = @polizaId
            SET pc.monto = @monto
            WHERE pc.numero_cuota = @numeroCuota
              AND (
                    pc.id = @cuotaId
                    OR (
                        source.cliente_contratante_id IS NOT NULL
                        AND p.cliente_contratante_id = source.cliente_contratante_id
                        AND UPPER(TRIM(COALESCE(p.aseguradora,''))) = UPPER(TRIM(COALESCE(source.aseguradora,'')))
                        AND UPPER(TRIM(COALESCE(p.numero_poliza,''))) = UPPER(TRIM(COALESCE(source.numero_poliza,'')))
                        AND UPPER(TRIM(COALESCE(p.ramo,''))) = UPPER(TRIM(COALESCE(source.ramo,'')))
                    )
              );", new { cuotaId, polizaId, numeroCuota, monto }, tx);

        await cn.ExecuteAsync(@"
            UPDATE poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN polizas source ON source.id = @polizaId
            LEFT JOIN (
                SELECT cuota_id, COALESCE(SUM(monto),0) monto_pagado
                FROM poliza_pagos
                WHERE activo = 1
                GROUP BY cuota_id
            ) pagos ON pagos.cuota_id = pc.id
            SET pc.estado = CASE
                    WHEN COALESCE(pagos.monto_pagado,0) >= pc.monto AND pc.monto > 0 THEN 'PAGADA'
                    WHEN COALESCE(pagos.monto_pagado,0) > 0 AND COALESCE(pagos.monto_pagado,0) < pc.monto THEN 'PARCIAL'
                    WHEN pc.fecha_vencimiento < CURDATE() THEN 'VENCIDA'
                    ELSE 'PENDIENTE'
                END,
                pc.fecha_pago = CASE WHEN COALESCE(pagos.monto_pagado,0) > 0 THEN IFNULL(pc.fecha_pago, CURDATE()) ELSE NULL END
            WHERE pc.numero_cuota = @numeroCuota
              AND (
                    pc.id = @cuotaId
                    OR (
                        source.cliente_contratante_id IS NOT NULL
                        AND p.cliente_contratante_id = source.cliente_contratante_id
                        AND UPPER(TRIM(COALESCE(p.aseguradora,''))) = UPPER(TRIM(COALESCE(source.aseguradora,'')))
                        AND UPPER(TRIM(COALESCE(p.numero_poliza,''))) = UPPER(TRIM(COALESCE(source.numero_poliza,'')))
                        AND UPPER(TRIM(COALESCE(p.ramo,''))) = UPPER(TRIM(COALESCE(source.ramo,'')))
                    )
              );", new { cuotaId, polizaId, numeroCuota }, tx);

        await RecalcularEstadoPolizaAsync(polizaId, cn, tx);
        tx.Commit();
        return true;
    }

    private static async Task InsertVehicleHistoryIfChangedAsync(System.Data.IDbConnection cn, System.Data.IDbTransaction tx, int vehiculoId, string campo, string? anterior, string? nuevo, string? origen)
    {
        if (string.IsNullOrWhiteSpace(nuevo) || string.Equals(anterior?.Trim(), nuevo.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        await cn.ExecuteAsync(@"
            INSERT INTO vehiculo_historial (vehiculo_id, campo, valor_anterior, valor_nuevo, origen)
            VALUES (@vehiculoId, @campo, @anterior, @nuevo, @origen);",
            new { vehiculoId, campo, anterior, nuevo, origen }, tx);
    }

    private static async Task RecalcularEstadoPolizaAsync(int polizaId, System.Data.IDbConnection cn, System.Data.IDbTransaction tx)
    {
        var estado = await cn.ExecuteScalarAsync<string?>(@"
            SELECT CASE
                WHEN COUNT(*) = 0 THEN 'SIN_VALIDAR'
                WHEN SUM(CASE WHEN estado = 'PAGADA' THEN 1 ELSE 0 END) = COUNT(*) THEN 'PAGADO'
                WHEN SUM(CASE WHEN estado = 'VENCIDA' THEN 1 ELSE 0 END) > 0 THEN 'MORA'
                WHEN SUM(CASE WHEN estado = 'PARCIAL' THEN 1 ELSE 0 END) > 0 THEN 'PARCIAL'
                ELSE 'EN_CUOTAS'
            END
            FROM poliza_cuotas
            WHERE poliza_id = @polizaId;", new { polizaId }, tx);

        await cn.ExecuteAsync(@"
            UPDATE polizas
            SET estado_pago = @estado,
                origen_estado_pago = COALESCE(origen_estado_pago, 'REGLA_AUTOMATICA')
            WHERE id = @polizaId;", new { polizaId, estado }, tx);
    }

    // ── Chart data ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<dynamic>> GetPrimaMensualAsync(int meses = 12)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync(@"
            SELECT
                DATE_FORMAT(p.vigencia, '%Y-%m') AS mes,
                DATE_FORMAT(p.vigencia, '%b %Y') AS mesLabel,
                CAST(SUM(p.prima_total) AS DECIMAL(18,2)) AS prima,
                COUNT(DISTINCT IFNULL(NULLIF(p.numero_poliza,''), p.id)) AS polizas
            FROM polizas p
            WHERE p.activo = 1
              AND p.vigencia >= DATE_SUB(CURDATE(), INTERVAL @meses MONTH)
            GROUP BY DATE_FORMAT(p.vigencia, '%Y-%m'), DATE_FORMAT(p.vigencia, '%b %Y')
            ORDER BY mes ASC;", new { meses });
    }

    public async Task<IEnumerable<dynamic>> GetDistribucionAseguradorasAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync(@"
            SELECT
                IFNULL(NULLIF(TRIM(aseguradora),''), 'Sin aseguradora') AS aseguradora,
                COUNT(DISTINCT IFNULL(NULLIF(numero_poliza,''), id)) AS polizas,
                CAST(SUM(prima_total) AS DECIMAL(18,2)) AS prima
            FROM polizas
            WHERE activo = 1
            GROUP BY IFNULL(NULLIF(TRIM(aseguradora),''), 'Sin aseguradora')
            ORDER BY prima DESC
            LIMIT 10;");
    }

    public async Task<IEnumerable<dynamic>> GetDistribucionEstadosAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync(@"
            SELECT
                CASE
                    WHEN hasta < CURDATE()                                         THEN 'Vencida'
                    WHEN hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 7 DAY)  THEN 'Vence 7 días'
                    WHEN hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY) THEN 'Vence 30 días'
                    WHEN estado_pago IN ('MORA','VENCIDA')                         THEN 'En mora'
                    ELSE 'Activa'
                END AS estado,
                COUNT(DISTINCT IFNULL(NULLIF(numero_poliza,''), id)) AS total
            FROM polizas
            WHERE activo = 1
            GROUP BY 1
            ORDER BY total DESC;");
    }

    public async Task<IEnumerable<dynamic>> GetCuotasMensualesAsync(int meses = 6)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync(@"
            SELECT
                DATE_FORMAT(pc.fecha_vencimiento, '%Y-%m') AS mes,
                DATE_FORMAT(pc.fecha_vencimiento, '%b %Y') AS mesLabel,
                SUM(CASE WHEN pc.estado = 'PAGADA'   THEN 1 ELSE 0 END) AS pagadas,
                SUM(CASE WHEN pc.estado = 'PENDIENTE' THEN 1 ELSE 0 END) AS pendientes,
                SUM(CASE WHEN pc.estado = 'VENCIDA'  THEN 1 ELSE 0 END) AS vencidas,
                CAST(SUM(CASE WHEN pc.estado = 'PAGADA'  THEN pc.monto ELSE 0 END) AS DECIMAL(18,2)) AS montoPagado,
                CAST(SUM(CASE WHEN pc.estado != 'PAGADA' THEN pc.monto ELSE 0 END) AS DECIMAL(18,2)) AS montoPendiente
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id AND p.activo = 1
            WHERE pc.fecha_vencimiento >= DATE_SUB(CURDATE(), INTERVAL @meses MONTH)
              AND pc.fecha_vencimiento <= DATE_ADD(CURDATE(), INTERVAL 3 MONTH)
            GROUP BY DATE_FORMAT(pc.fecha_vencimiento, '%Y-%m'), DATE_FORMAT(pc.fecha_vencimiento, '%b %Y')
            ORDER BY mes ASC;", new { meses });
    }

    public async Task<IEnumerable<dynamic>> GetPolizasParaPdfAsync(
        string? aseguradora = null, string? ciudad = null,
        DateTime? vigenciaDesde = null, DateTime? vigenciaHasta = null,
        bool soloActivas = true)
    {
        using var cn = _factory.CreateConnection();
        var where = new List<string>();
        var p = new Dapper.DynamicParameters();

        if (soloActivas) where.Add("p.activo = 1");
        if (!string.IsNullOrWhiteSpace(aseguradora)) { where.Add("p.aseguradora = @aseg"); p.Add("aseg", aseguradora); }
        if (!string.IsNullOrWhiteSpace(ciudad))      { where.Add("c.ciudad = @ciudad");   p.Add("ciudad", ciudad); }
        if (vigenciaDesde.HasValue)                   { where.Add("p.vigencia >= @vd");    p.Add("vd", vigenciaDesde); }
        if (vigenciaHasta.HasValue)                   { where.Add("p.vigencia <= @vh");    p.Add("vh", vigenciaHasta); }

        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        return await cn.QueryAsync($@"
            SELECT c.nombre clienteNombre, p.numero_poliza numeroPoliza,
                   p.aseguradora, p.ramo, p.vigencia, p.hasta,
                   COALESCE(p.prima_total, 0) primaTotal, p.estado_poliza_real estadoPolizaReal
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            {wClause}
            ORDER BY c.nombre, p.hasta;", p);
    }

    // ── Global search ────────────────────────────────────────────────────────

    public async Task<dynamic> BuscarAsync(string q, int limit = 10)
    {
        using var cn = _factory.CreateConnection();
        var like = $"%{q}%";
        var p = new { like, limit };

        var clientes = await cn.QueryAsync(@"
            SELECT id, nombre, telefono, 'cliente' AS tipo,
                   (SELECT COUNT(*) FROM polizas px WHERE px.cliente_id = c.id AND px.activo = 1) AS polizasActivas
            FROM clientes c
            WHERE activo = 1
              AND (nombre LIKE @like OR telefono LIKE @like OR email LIKE @like OR cedula LIKE @like)
            ORDER BY nombre LIMIT @limit;", p);

        var polizas = await cn.QueryAsync(@"
            SELECT p.id, p.numero_poliza AS codigo, c.nombre AS cliente,
                   p.aseguradora, p.hasta, 'poliza' AS tipo,
                   p.activo, p.estado_poliza_real AS estado
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE (p.numero_poliza LIKE @like OR c.nombre LIKE @like OR p.aseguradora LIKE @like
                   OR p.certificado LIKE @like OR p.placa LIKE @like OR p.vehiculo LIKE @like)
            ORDER BY p.activo DESC, p.hasta DESC
            LIMIT @limit;", p);

        return new { clientes, polizas };
    }

    /// <summary>
    /// Recalcula estado_negocio de todos los clientes en función de sus pólizas vigentes.
    /// PROSPECTO: sin pólizas · EN_RIESGO: pólizas pero ninguna vigente · ACTIVO: tiene al menos 1 vigente · INACTIVO: activo=0.
    /// </summary>
    public async Task<int> SincronizarEstadosClientesAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteAsync(@"
            UPDATE clientes c
            SET estado_negocio = CASE
                WHEN c.activo = 0
                    THEN 'INACTIVO'
                WHEN NOT EXISTS (
                    SELECT 1 FROM polizas p WHERE p.cliente_id = c.id
                )
                    THEN 'PROSPECTO'
                WHEN NOT EXISTS (
                    SELECT 1 FROM polizas p
                    WHERE p.cliente_id = c.id
                      AND p.activo = 1
                      AND (p.hasta IS NULL OR p.hasta >= CURDATE())
                )
                    THEN 'EN_RIESGO'
                ELSE 'ACTIVO'
            END
            WHERE estado_negocio <> CASE
                WHEN c.activo = 0
                    THEN 'INACTIVO'
                WHEN NOT EXISTS (
                    SELECT 1 FROM polizas p WHERE p.cliente_id = c.id
                )
                    THEN 'PROSPECTO'
                WHEN NOT EXISTS (
                    SELECT 1 FROM polizas p
                    WHERE p.cliente_id = c.id
                      AND p.activo = 1
                      AND (p.hasta IS NULL OR p.hasta >= CURDATE())
                )
                    THEN 'EN_RIESGO'
                ELSE 'ACTIVO'
            END;");
    }

    /// <summary>
    /// Devuelve todos los clientes (sin paginación) con sus pólizas agregadas, para exportar a Excel.
    /// Acepta los mismos filtros que GetClientesListadoAsync pero sin paginación.
    /// </summary>
    public async Task<IEnumerable<ClienteListado>> GetClientesParaExcelAsync(
        string? buscar = null,
        string? estado = null,
        string? financiera = null,
        string? aseguradora = null,
        string? ramo = null,
        string? estadoPago = null,
        string? ciudad = null)
    {
        await EnsureImportSchemaAsync();
        using var cn = _factory.CreateConnection();

        var where = new List<string>();
        var parameters = new DynamicParameters();

        where.Add(@"NOT (
            NOT EXISTS (SELECT 1 FROM polizas px WHERE px.cliente_id = c.id)
            AND EXISTS (SELECT 1 FROM polizas px WHERE px.cliente_contratante_id = c.id)
        )");

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            where.Add(@"(c.nombre LIKE @buscar OR c.telefono LIKE @buscar OR c.email LIKE @buscar
                OR EXISTS (SELECT 1 FROM polizas px WHERE px.cliente_id = c.id
                    AND (px.numero_poliza LIKE @buscar OR px.aseguradora LIKE @buscar OR px.ramo LIKE @buscar)))");
            parameters.Add("buscar", $"%{buscar.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(financiera))
        {
            where.Add(@"EXISTS (SELECT 1 FROM polizas pf LEFT JOIN clientes cf ON cf.id = pf.cliente_contratante_id
                WHERE pf.cliente_id = c.id AND (cf.nombre LIKE @financiera OR pf.observaciones LIKE @financiera))");
            parameters.Add("financiera", $"%{financiera.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(aseguradora))
        {
            where.Add("EXISTS (SELECT 1 FROM polizas pa WHERE pa.cliente_id = c.id AND pa.aseguradora LIKE @aseguradora)");
            parameters.Add("aseguradora", $"%{aseguradora.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(ramo))
        {
            where.Add("EXISTS (SELECT 1 FROM polizas pr WHERE pr.cliente_id = c.id AND (pr.ramo LIKE @ramo OR pr.ramo_normalizado LIKE @ramo))");
            parameters.Add("ramo", $"%{ramo.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(estadoPago))
        {
            where.Add("EXISTS (SELECT 1 FROM polizas pp WHERE pp.cliente_id = c.id AND pp.estado_pago = @estadoPago)");
            parameters.Add("estadoPago", estadoPago.Trim());
        }
        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            where.Add("c.ciudad LIKE @ciudad");
            parameters.Add("ciudad", $"%{ciudad.Trim()}%");
        }
        if (estado == "ACTIVO")       where.Add("c.activo = 1");
        else if (estado == "INACTIVO") where.Add("c.activo = 0");

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        var sql = $@"
            SELECT
                c.id Id, c.nombre Nombre, c.telefono Telefono, c.email Email, c.ciudad Ciudad,
                c.activo Activo, c.estado_revision EstadoRevision, c.motivo_revision MotivoRevision,
                c.requiere_revision_manual RequiereRevisionManual, c.fecha_creacion FechaCreacion,
                COALESCE(c.estado_negocio,'ACTIVO') EstadoNegocio,
                COUNT(p.id) Polizas,
                COALESCE(SUM(CASE WHEN p.activo = 1 THEN 1 ELSE 0 END), 0) PolizasActivas
            FROM clientes c
            LEFT JOIN polizas p ON p.cliente_id = c.id
            {whereSql}
            GROUP BY c.id, c.nombre, c.telefono, c.email, c.ciudad, c.activo,
                     c.estado_revision, c.motivo_revision, c.requiere_revision_manual,
                     c.fecha_creacion, c.estado_negocio
            ORDER BY c.nombre
            LIMIT 5000;";

        return await cn.QueryAsync<ClienteListado>(sql, parameters);
    }

    /// <summary>
    /// Devuelve las tareas operativas urgentes para el panel "Tareas de hoy":
    /// pólizas que vencen en 2 días, cuotas vencidas sin pagar y reclamos con documentos pendientes.
    /// </summary>
    public async Task<dynamic> GetTareasHoyAsync()
    {
        using var cn = _factory.CreateConnection();

        var polizasVencen = await cn.QueryAsync(@"
            SELECT p.id id, c.nombre cliente, c.telefono telefono,
                   p.numero_poliza numeroPoliza, p.aseguradora aseguradora, p.ramo ramo,
                   p.hasta hasta, DATEDIFF(p.hasta, CURDATE()) diasRestantes
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE p.activo = 1
              AND p.hasta IS NOT NULL
              AND p.hasta BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 2 DAY)
            ORDER BY p.hasta
            LIMIT 20;");

        var cuotasVencidas = await cn.QueryAsync(@"
            SELECT pc.id id, c.nombre cliente, c.telefono telefono,
                   p.numero_poliza numeroPoliza, p.aseguradora aseguradora,
                   pc.numero_cuota numeroCuota, pc.fecha_vencimiento fechaVencimiento,
                   pc.monto monto, DATEDIFF(CURDATE(), pc.fecha_vencimiento) diasVencida
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE pc.estado = 'VENCIDA'
              AND p.activo = 1
            ORDER BY pc.fecha_vencimiento
            LIMIT 20;");

        var reclamosPendientes = await cn.QueryAsync(@"
            SELECT r.id id,
                   COALESCE(c.nombre, r.asegurado) cliente,
                   r.aseguradora aseguradora, r.asegurado asegurado,
                   r.estado estado, r.fecha_creacion fechaCreacion
            FROM reclamos_whatsapp r
            LEFT JOIN clientes c ON c.id = r.cliente_id
            WHERE r.estado = 'DOCUMENTOS_PENDIENTES'
            ORDER BY r.fecha_creacion
            LIMIT 20;");

        return new { polizasVencen, cuotasVencidas, reclamosPendientes };
    }

    /// <summary>
    /// Recalcula estado_poliza_real para todas las pólizas activas basándose en fecha real.
    /// Transiciones automáticas: ACTIVA → VENCIDA cuando hasta &lt; hoy.
    /// Solo actualiza pólizas donde el estado calculado difiere del almacenado.
    /// </summary>
    public async Task<int> SincronizarEstadosPolizasAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteAsync(@"
            UPDATE polizas
            SET estado_poliza_real = CASE
                    WHEN activo = 0                                                         THEN estado_poliza_real  -- no tocar manuales cancelados
                    WHEN hasta < CURDATE()
                         AND estado_poliza_real NOT IN ('CANCELADA','NO_RENOVADA',
                                                        'PENDIENTE_CANCELACION','VENCIDA')  THEN 'VENCIDA'
                    ELSE estado_poliza_real
                END,
                fecha_actualizacion = CASE
                    WHEN activo = 1
                         AND hasta < CURDATE()
                         AND estado_poliza_real NOT IN ('CANCELADA','NO_RENOVADA',
                                                        'PENDIENTE_CANCELACION','VENCIDA')  THEN NOW()
                    ELSE fecha_actualizacion
                END
            WHERE activo = 1
              AND hasta < CURDATE()
              AND estado_poliza_real NOT IN ('CANCELADA','NO_RENOVADA','PENDIENTE_CANCELACION','VENCIDA');");
    }
}
