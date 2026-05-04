using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class PagoRepository
{
    private readonly DbConnectionFactory _factory;

    public PagoRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> GenerarCuotasAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string polizasSql = @"
            SELECT
                id Id,
                cliente_id ClienteId,
                cuotas Cuotas,
                prima_total PrimaTotal,
                vigencia Vigencia,
                hasta Hasta
            FROM polizas
            WHERE activo = 1
              AND cuotas IS NOT NULL
              AND cuotas > 0
              AND prima_total IS NOT NULL
              AND prima_total > 0;";

        var polizas = await cn.QueryAsync<Poliza>(polizasSql);
        var creadas = 0;

        const string insertSql = @"
            INSERT IGNORE INTO poliza_cuotas
            (poliza_id, numero_cuota, fecha_vencimiento, monto, estado)
            VALUES
            (@PolizaId, @NumeroCuota, @FechaVencimiento, @Monto, 'PENDIENTE');";

        foreach (var poliza in polizas)
        {
            var cantidad = Math.Min(poliza.Cuotas ?? 0, 12);
            if (cantidad <= 0 || poliza.PrimaTotal is null)
                continue;

            var monto = Math.Round(poliza.PrimaTotal.Value / cantidad, 2, MidpointRounding.AwayFromZero);
            var primeraFecha = CalcularPrimeraFecha(poliza);

            for (var i = 1; i <= cantidad; i++)
            {
                var montoCuota = i == cantidad
                    ? Math.Round(poliza.PrimaTotal.Value - (monto * (cantidad - 1)), 2, MidpointRounding.AwayFromZero)
                    : monto;
                var affected = await cn.ExecuteAsync(insertSql, new
                {
                    PolizaId = poliza.Id,
                    NumeroCuota = i,
                    FechaVencimiento = primeraFecha.AddMonths(i - 1),
                    Monto = montoCuota
                });

                creadas += affected;
            }
        }

        return creadas;
    }

    public async Task<(IEnumerable<PolizaCuota> items, int total)> GetCuotasAsync(string? estado = "PENDIENTE", string? buscar = null, int pagina = 1, int pageSize = 25)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var where = new List<string>();
        var parameters = new DynamicParameters();
        pagina = pagina < 1 ? 1 : pagina;
        pageSize = pageSize is < 10 or > 100 ? 25 : pageSize;

        var estadoNormalizado = (estado ?? "").Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(estadoNormalizado) && estadoNormalizado != "TODOS")
        {
            if (estadoNormalizado == "HOY")
            {
                where.Add("pc.fecha_vencimiento = CURDATE()");
            }
            else if (estadoNormalizado == "PROXIMOS_7")
            {
                where.Add("pc.fecha_vencimiento BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 7 DAY)");
                where.Add("pc.estado IN ('PENDIENTE','PARCIAL')");
            }
            else
            {
                where.Add("pc.estado = @estado");
                parameters.Add("estado", estadoNormalizado);
            }
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            where.Add("(c.nombre LIKE @buscar OR p.numero_poliza LIKE @buscar OR c.telefono LIKE @buscar)");
            parameters.Add("buscar", $"%{buscar.Trim()}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        parameters.Add("limit", pageSize);
        parameters.Add("offset", (pagina - 1) * pageSize);

        var sql = $@"
            SELECT
                pc.id Id,
                pc.poliza_id PolizaId,
                p.cliente_id ClienteId,
                c.nombre Cliente,
                COALESCE(NULLIF(c.telefono, ''), ct.telefono) Telefono,
                p.numero_poliza NumeroPoliza,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                pc.numero_cuota NumeroCuota,
                pc.fecha_vencimiento FechaVencimiento,
                pc.monto Monto,
                COALESCE((SELECT SUM(pp.monto) FROM poliza_pagos pp WHERE pp.cuota_id = pc.id AND pp.activo = 1), 0) MontoPagado,
                (SELECT pp.metodo_pago FROM poliza_pagos pp WHERE pp.cuota_id = pc.id AND pp.activo = 1 ORDER BY pp.fecha_pago DESC, pp.id DESC LIMIT 1) MetodoPago,
                pc.estado Estado,
                pc.fecha_pago FechaPago,
                pc.comprobante_url ComprobanteUrl,
                pc.documento_id DocumentoId,
                pc.numero_recibo NumeroRecibo,
                COALESCE(pc.referencia_banco, (SELECT pp.referencia_banco FROM poliza_pagos pp WHERE pp.cuota_id = pc.id AND pp.activo = 1 ORDER BY pp.fecha_pago DESC, pp.id DESC LIMIT 1)) ReferenciaBanco,
                pc.observaciones Observaciones,
                pc.fecha_creacion FechaCreacion
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN cliente_telefonos ct ON ct.cliente_id = c.id AND ct.principal = 1 AND ct.activo = 1
            {whereSql}
            ORDER BY
                CASE pc.estado WHEN 'VENCIDA' THEN 0 WHEN 'PENDIENTE' THEN 1 WHEN 'PAGADA' THEN 2 ELSE 3 END,
                pc.fecha_vencimiento ASC,
                c.nombre ASC
            LIMIT @limit OFFSET @offset;";

        var countSql = $@"
            SELECT COUNT(*)
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN cliente_telefonos ct ON ct.cliente_id = c.id AND ct.principal = 1 AND ct.activo = 1
            {whereSql};";

        var items = await cn.QueryAsync<PolizaCuota>(sql, parameters);
        var total = await cn.ExecuteScalarAsync<int>(countSql, parameters);

        return (items, total);
    }

    public async Task ActualizarEstadosVencidosAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE poliza_cuotas pc
            LEFT JOIN (
                SELECT cuota_id, COALESCE(SUM(monto),0) monto_pagado
                FROM poliza_pagos
                WHERE activo = 1
                GROUP BY cuota_id
            ) pagos ON pagos.cuota_id = pc.id
            SET pc.estado = CASE
                    WHEN COALESCE(pagos.monto_pagado,0) >= pc.monto THEN 'PAGADA'
                    WHEN COALESCE(pagos.monto_pagado,0) > 0 AND COALESCE(pagos.monto_pagado,0) < pc.monto THEN 'PARCIAL'
                    WHEN pc.fecha_vencimiento < CURDATE() THEN 'VENCIDA'
                    ELSE 'PENDIENTE'
                END,
                pc.fecha_pago = CASE WHEN COALESCE(pagos.monto_pagado,0) > 0 THEN IFNULL(pc.fecha_pago, CURDATE()) ELSE NULL END;";

        await cn.ExecuteAsync(sql);
        await cn.ExecuteAsync(@"
            UPDATE polizas p
            LEFT JOIN (
                SELECT poliza_id,
                       COUNT(*) total,
                       SUM(CASE WHEN estado = 'PAGADA' THEN 1 ELSE 0 END) pagadas,
                       SUM(CASE WHEN estado = 'VENCIDA' THEN 1 ELSE 0 END) vencidas,
                       SUM(CASE WHEN estado = 'PARCIAL' THEN 1 ELSE 0 END) parciales
                FROM poliza_cuotas
                GROUP BY poliza_id
            ) q ON q.poliza_id = p.id
            SET p.estado_pago = CASE
                    WHEN COALESCE(q.total,0) = 0 THEN p.estado_pago
                    WHEN q.pagadas = q.total THEN 'PAGADO'
                    WHEN q.vencidas > 0 THEN 'MORA'
                    WHEN q.parciales > 0 THEN 'PARCIAL'
                    ELSE 'EN_CUOTAS'
                END
            WHERE q.poliza_id IS NOT NULL;");
    }

    public async Task<IEnumerable<object>> GetPolizasSinCuotasAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                p.id PolizaId,
                p.numero_poliza NumeroPoliza,
                c.nombre Cliente,
                p.cuotas Cuotas,
                p.prima_total PrimaTotal
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN poliza_cuotas pc ON pc.poliza_id = p.id
            WHERE p.activo = 1
            GROUP BY p.id, p.numero_poliza, c.nombre, p.cuotas, p.prima_total
            HAVING COUNT(pc.id) = 0
            ORDER BY c.nombre ASC, p.numero_poliza ASC
            LIMIT 25;";

        return await cn.QueryAsync(sql);
    }

    public async Task MarcarPagadaAsync(int id, DateTime fechaPago, string? observaciones)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE poliza_cuotas
            SET estado = 'PAGADA',
                fecha_pago = @fechaPago,
                observaciones = @observaciones
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new { id, fechaPago, observaciones });
    }

    public async Task<dynamic> GetStatsAsync(string? aseguradora = null, string? ciudad = null, DateTime? desde = null, DateTime? hasta = null)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();

        var where = new List<string>();
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(aseguradora))
        {
            where.Add("p.aseguradora LIKE @aseguradora");
            parameters.Add("aseguradora", $"%{aseguradora.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            where.Add("c.ciudad LIKE @ciudad");
            parameters.Add("ciudad", $"%{ciudad.Trim()}%");
        }
        if (desde.HasValue)
        {
            where.Add("pc.fecha_vencimiento >= @desde");
            parameters.Add("desde", desde.Value.Date);
        }
        if (hasta.HasValue)
        {
            where.Add("pc.fecha_vencimiento <= @hasta");
            parameters.Add("hasta", hasta.Value.Date);
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var sql = $@"
            SELECT
                COALESCE(SUM(CASE WHEN estado = 'PENDIENTE' THEN 1 ELSE 0 END), 0) Pendientes,
                COALESCE(SUM(CASE WHEN estado = 'VENCIDA' THEN 1 ELSE 0 END), 0) Vencidas,
                COALESCE(SUM(CASE WHEN estado = 'PAGADA' THEN 1 ELSE 0 END), 0) Pagadas,
                COALESCE(SUM(CASE WHEN estado = 'PARCIAL' THEN 1 ELSE 0 END), 0) Parciales,
                COALESCE(SUM(CASE WHEN estado IN ('PENDIENTE','VENCIDA') THEN monto ELSE 0 END), 0) MontoPendiente,
                COALESCE(SUM(CASE WHEN estado = 'VENCIDA' THEN monto ELSE 0 END), 0) MontoVencido
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            {whereSql};";

        return await cn.QueryFirstAsync(sql, parameters);
    }

    private static DateTime CalcularPrimeraFecha(Poliza poliza)
    {
        if (poliza.Vigencia.HasValue)
            return poliza.Vigencia.Value.Date;

        if (poliza.Hasta.HasValue && poliza.Cuotas.HasValue)
            return poliza.Hasta.Value.Date.AddMonths(-(poliza.Cuotas.Value - 1));

        return DateTime.Today;
    }

    public async Task<int> RegistrarPagoAsync(int cuotaId, PolizaPago pago)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        using var tx = cn.BeginTransaction();

        const string insertSql = @"
            INSERT INTO poliza_pagos
            (cuota_id, monto, fecha_pago, metodo_pago, documento_id, numero_recibo, referencia_banco, observaciones, registrado_por_usuario_id, activo)
            VALUES
            (@CuotaId, @Monto, @FechaPago, @MetodoPago, @DocumentoId, @NumeroRecibo, @ReferenciaBanco, @Observaciones, @RegistradoPorUsuarioId, 1);
            SELECT LAST_INSERT_ID();";
        var pagoId = await cn.ExecuteScalarAsync<int>(insertSql, new
        {
            CuotaId = cuotaId,
            pago.Monto,
            FechaPago = pago.FechaPago == default ? DateTime.Today : pago.FechaPago,
            MetodoPago = string.IsNullOrWhiteSpace(pago.MetodoPago) ? "OTRO" : pago.MetodoPago,
            pago.DocumentoId,
            pago.NumeroRecibo,
            pago.ReferenciaBanco,
            pago.Observaciones,
            pago.RegistradoPorUsuarioId
        }, tx);

        await RecalcularEstadoCuotaAsync(cuotaId, cn, tx);
        await RecalcularEstadoPolizaPorCuotaAsync(cuotaId, cn, tx);
        tx.Commit();
        return pagoId;
    }

    public async Task<IEnumerable<PolizaPago>> GetPagosByCuotaAsync(int cuotaId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT
                id Id,
                cuota_id CuotaId,
                monto Monto,
                fecha_pago FechaPago,
                metodo_pago MetodoPago,
                documento_id DocumentoId,
                numero_recibo NumeroRecibo,
                referencia_banco ReferenciaBanco,
                observaciones Observaciones,
                registrado_por_usuario_id RegistradoPorUsuarioId,
                fecha_creacion FechaCreacion,
                activo Activo
            FROM poliza_pagos
            WHERE cuota_id = @cuotaId
              AND activo = 1
            ORDER BY fecha_pago DESC, id DESC;";
        return await cn.QueryAsync<PolizaPago>(sql, new { cuotaId });
    }

    private static async Task RecalcularEstadoCuotaAsync(int cuotaId, System.Data.IDbConnection cn, System.Data.IDbTransaction tx)
    {
        const string sql = @"
            UPDATE poliza_cuotas pc
            LEFT JOIN (
                SELECT cuota_id, COALESCE(SUM(monto),0) monto_pagado
                FROM poliza_pagos
                WHERE activo = 1
                GROUP BY cuota_id
            ) pagos ON pagos.cuota_id = pc.id
            SET pc.estado = CASE
                    WHEN COALESCE(pagos.monto_pagado,0) >= pc.monto THEN 'PAGADA'
                    WHEN COALESCE(pagos.monto_pagado,0) > 0 AND COALESCE(pagos.monto_pagado,0) < pc.monto THEN 'PARCIAL'
                    WHEN pc.fecha_vencimiento < CURDATE() THEN 'VENCIDA'
                    ELSE 'PENDIENTE'
                END,
                pc.fecha_pago = CASE WHEN COALESCE(pagos.monto_pagado,0) > 0 THEN IFNULL(pc.fecha_pago, CURDATE()) ELSE NULL END
            WHERE pc.id = @cuotaId;";
        await cn.ExecuteAsync(sql, new { cuotaId }, tx);
    }

    private static async Task RecalcularEstadoPolizaPorCuotaAsync(int cuotaId, System.Data.IDbConnection cn, System.Data.IDbTransaction tx)
    {
        const string sql = @"
            UPDATE polizas p
            INNER JOIN poliza_cuotas target ON target.poliza_id = p.id AND target.id = @cuotaId
            INNER JOIN (
                SELECT poliza_id,
                       COUNT(*) total,
                       SUM(CASE WHEN estado = 'PAGADA' THEN 1 ELSE 0 END) pagadas,
                       SUM(CASE WHEN estado = 'VENCIDA' THEN 1 ELSE 0 END) vencidas,
                       SUM(CASE WHEN estado = 'PARCIAL' THEN 1 ELSE 0 END) parciales
                FROM poliza_cuotas
                WHERE poliza_id = (SELECT poliza_id FROM poliza_cuotas WHERE id = @cuotaId)
                GROUP BY poliza_id
            ) q ON q.poliza_id = p.id
            SET p.estado_pago = CASE
                    WHEN q.pagadas = q.total THEN 'PAGADO'
                    WHEN q.vencidas > 0 THEN 'MORA'
                    WHEN q.parciales > 0 THEN 'PARCIAL'
                    ELSE 'EN_CUOTAS'
                END
            WHERE p.id = q.poliza_id;";
        await cn.ExecuteAsync(sql, new { cuotaId }, tx);
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            ALTER TABLE poliza_cuotas
                ADD COLUMN IF NOT EXISTS documento_id INT NULL,
                ADD COLUMN IF NOT EXISTS numero_recibo VARCHAR(80) NULL,
                ADD COLUMN IF NOT EXISTS referencia_banco VARCHAR(120) NULL;

            CREATE TABLE IF NOT EXISTS poliza_pagos (
                id INT AUTO_INCREMENT PRIMARY KEY,
                cuota_id INT NOT NULL,
                monto DECIMAL(18,2) NOT NULL,
                fecha_pago DATE NOT NULL,
                metodo_pago VARCHAR(40) NOT NULL DEFAULT 'OTRO',
                documento_id INT NULL,
                numero_recibo VARCHAR(80) NULL,
                referencia_banco VARCHAR(120) NULL,
                observaciones TEXT NULL,
                registrado_por_usuario_id INT NULL,
                fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                INDEX ix_poliza_pagos_cuota (cuota_id),
                INDEX ix_poliza_pagos_fecha (fecha_pago),
                CONSTRAINT fk_poliza_pagos_cuota FOREIGN KEY (cuota_id) REFERENCES poliza_cuotas(id) ON DELETE CASCADE
            );");
    }
}
