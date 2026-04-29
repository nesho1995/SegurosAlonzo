using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class RecordatorioRepository
{
    private readonly DbConnectionFactory _factory;

    public RecordatorioRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(IEnumerable<Recordatorio> items, int total)> GetAsync(RecordatorioFiltro filtro)
    {
        using var cn = _factory.CreateConnection();

        filtro.Pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        filtro.PageSize = filtro.PageSize is < 10 or > 100 ? 25 : filtro.PageSize;

        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            where.Add("r.estado = @estado");
            parameters.Add("estado", filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            where.Add("r.tipo = @tipo");
            parameters.Add("tipo", filtro.Tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            where.Add("(c.nombre LIKE @buscar OR p.numero_poliza LIKE @buscar OR r.asunto LIKE @buscar OR r.telefono LIKE @buscar)");
            parameters.Add("buscar", $"%{filtro.Buscar.Trim()}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var offset = (filtro.Pagina - 1) * filtro.PageSize;
        parameters.Add("limit", filtro.PageSize);
        parameters.Add("offset", offset);

        var sql = $@"
            SELECT
                r.id Id,
                r.tipo Tipo,
                r.referencia Referencia,
                r.cliente_id ClienteId,
                r.poliza_id PolizaId,
                r.cuota_id CuotaId,
                c.nombre Cliente,
                r.telefono Telefono,
                p.numero_poliza NumeroPoliza,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                r.fecha_objetivo FechaObjetivo,
                r.asunto Asunto,
                r.mensaje Mensaje,
                r.estado Estado,
                r.fecha_creacion FechaCreacion,
                r.fecha_envio FechaEnvio,
                r.error Error
            FROM recordatorios r
            INNER JOIN clientes c ON c.id = r.cliente_id
            LEFT JOIN polizas p ON p.id = r.poliza_id
            {whereSql}
            ORDER BY
                CASE r.estado WHEN 'PENDIENTE' THEN 0 WHEN 'ERROR' THEN 1 WHEN 'ENVIADO' THEN 2 ELSE 3 END,
                r.fecha_objetivo ASC,
                r.id DESC
            LIMIT @limit OFFSET @offset;";

        var countSql = $@"
            SELECT COUNT(*)
            FROM recordatorios r
            INNER JOIN clientes c ON c.id = r.cliente_id
            LEFT JOIN polizas p ON p.id = r.poliza_id
            {whereSql};";

        var items = await cn.QueryAsync<Recordatorio>(sql, parameters);
        var total = await cn.ExecuteScalarAsync<int>(countSql, parameters);

        return (items, total);
    }

    public async Task<Recordatorio?> GetByIdAsync(int id)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                r.id Id,
                r.tipo Tipo,
                r.referencia Referencia,
                r.cliente_id ClienteId,
                r.poliza_id PolizaId,
                r.cuota_id CuotaId,
                c.nombre Cliente,
                r.telefono Telefono,
                p.numero_poliza NumeroPoliza,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                r.fecha_objetivo FechaObjetivo,
                r.asunto Asunto,
                r.mensaje Mensaje,
                r.estado Estado,
                r.fecha_creacion FechaCreacion,
                r.fecha_envio FechaEnvio,
                r.error Error
            FROM recordatorios r
            INNER JOIN clientes c ON c.id = r.cliente_id
            LEFT JOIN polizas p ON p.id = r.poliza_id
            WHERE r.id = @id;";

        return await cn.QueryFirstOrDefaultAsync<Recordatorio>(sql, new { id });
    }

    public async Task<int> GenerarPendientesAsync()
    {
        return await GenerarPendientesAsync(new EnvioAutomaticoConfig());
    }

    public async Task<int> GenerarPendientesAsync(EnvioAutomaticoConfig config)
    {
        using var cn = _factory.CreateConnection();
        var total = 0;
        var diasPoliza = ParseDias(config.DiasAntesVencimientoPoliza, 180, new[] { 30, 15, 7 });
        var diasCuotaAntes = ParseDias(config.DiasAntesVencimientoCuota, 90, new[] { 7, 3, 1 });
        var diasCuotaDespues = ParseDias(config.DiasDespuesCuotaVencida, 90, new[] { 1, 3, 7, 15 });

        total += await cn.ExecuteAsync(@"
            INSERT IGNORE INTO recordatorios
            (tipo, referencia, cliente_id, poliza_id, telefono, fecha_objetivo, asunto, mensaje, estado)
            SELECT
                'RENOVACION',
                'RENOVACION_PROXIMA',
                c.id,
                p.id,
                COALESCE(NULLIF(c.telefono, ''), ct.telefono),
                p.hasta,
                CONCAT('Renovacion proxima - poliza ', IFNULL(p.numero_poliza, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@plantillaPoliza, '{cliente}', c.nombre), '{poliza}', IFNULL(p.numero_poliza, '')), '{aseguradora}', IFNULL(p.aseguradora, '')), '{fecha_vencimiento}', DATE_FORMAT(p.hasta, '%d/%m/%Y')), '{dias}', DATEDIFF(p.hasta, CURDATE())),
                'PENDIENTE'
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN cliente_telefonos ct ON ct.cliente_id = c.id AND ct.principal = 1 AND ct.activo = 1
            WHERE p.activo = 1
              AND c.activo = 1
              AND DATEDIFF(p.hasta, CURDATE()) IN @diasPoliza;",
            new { diasPoliza, plantillaPoliza = config.PlantillaPolizaPorVencer });

        total += await cn.ExecuteAsync(@"
            INSERT IGNORE INTO recordatorios
            (tipo, referencia, cliente_id, poliza_id, telefono, fecha_objetivo, asunto, mensaje, estado)
            SELECT
                'VENCIMIENTO',
                'POLIZA_VENCIDA',
                c.id,
                p.id,
                COALESCE(NULLIF(c.telefono, ''), ct.telefono),
                p.hasta,
                CONCAT('Poliza vencida - ', IFNULL(p.numero_poliza, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(@plantillaPolizaVencida, '{cliente}', c.nombre), '{poliza}', IFNULL(p.numero_poliza, '')), '{aseguradora}', IFNULL(p.aseguradora, '')), '{fecha_vencimiento}', DATE_FORMAT(p.hasta, '%d/%m/%Y')),
                'PENDIENTE'
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN cliente_telefonos ct ON ct.cliente_id = c.id AND ct.principal = 1 AND ct.activo = 1
            WHERE p.activo = 1
              AND c.activo = 1
              AND p.hasta < CURDATE()
              AND ABS(DATEDIFF(p.hasta, CURDATE())) IN @diasPolizaVencida;",
            new { plantillaPolizaVencida = config.PlantillaPolizaVencida, diasPolizaVencida = diasPoliza });

        total += await cn.ExecuteAsync(@"
            INSERT IGNORE INTO recordatorios
            (tipo, referencia, cliente_id, poliza_id, cuota_id, telefono, fecha_objetivo, asunto, mensaje, estado)
            SELECT
                'PAGO',
                CONCAT('PAGO_CUOTA_', pc.id),
                c.id,
                p.id,
                pc.id,
                COALESCE(NULLIF(c.telefono, ''), ct.telefono),
                pc.fecha_vencimiento,
                CONCAT('Pago pendiente - poliza ', IFNULL(p.numero_poliza, ''), ' cuota ', pc.numero_cuota),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    CASE WHEN pc.fecha_vencimiento < CURDATE() THEN @plantillaPagoVencido ELSE @plantillaPagoProximo END,
                    '{cliente}', c.nombre),
                    '{poliza}', IFNULL(p.numero_poliza, '')),
                    '{fecha_vencimiento}', DATE_FORMAT(pc.fecha_vencimiento, '%d/%m/%Y')),
                    '{monto}', CONCAT('L ', FORMAT(pc.monto, 2))),
                    '{dias}', ABS(DATEDIFF(pc.fecha_vencimiento, CURDATE()))),
                'PENDIENTE'
            FROM poliza_cuotas pc
            INNER JOIN polizas p ON p.id = pc.poliza_id
            INNER JOIN clientes c ON c.id = p.cliente_id
            LEFT JOIN cliente_telefonos ct ON ct.cliente_id = c.id AND ct.principal = 1 AND ct.activo = 1
            WHERE p.activo = 1
              AND c.activo = 1
              AND pc.estado IN ('PENDIENTE', 'VENCIDA')
              AND (
                    (pc.fecha_vencimiento >= CURDATE() AND DATEDIFF(pc.fecha_vencimiento, CURDATE()) IN @diasCuotaAntes)
                    OR
                    (pc.fecha_vencimiento < CURDATE() AND ABS(DATEDIFF(pc.fecha_vencimiento, CURDATE())) IN @diasCuotaDespues)
              );",
            new
            {
                diasCuotaAntes,
                diasCuotaDespues,
                plantillaPagoProximo = config.PlantillaPagoProximo,
                plantillaPagoVencido = config.PlantillaPagoVencido
            });

        return total;
    }

    private static int[] ParseDias(string? value, int max, int[] fallback)
    {
        var dias = (value ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var day) ? Math.Clamp(day, 0, max) : -1)
            .Where(x => x >= 0)
            .Distinct()
            .ToArray();
        return dias.Length == 0 ? fallback : dias;
    }

    public async Task<IEnumerable<Recordatorio>> GetPendientesParaAutoEnvioAsync(int limit = 100)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                r.id Id,
                r.tipo Tipo,
                r.referencia Referencia,
                r.cliente_id ClienteId,
                r.poliza_id PolizaId,
                r.cuota_id CuotaId,
                c.nombre Cliente,
                r.telefono Telefono,
                p.numero_poliza NumeroPoliza,
                p.aseguradora Aseguradora,
                p.ramo Ramo,
                r.fecha_objetivo FechaObjetivo,
                r.asunto Asunto,
                r.mensaje Mensaje,
                r.estado Estado,
                r.fecha_creacion FechaCreacion,
                r.fecha_envio FechaEnvio,
                r.error Error
            FROM recordatorios r
            INNER JOIN clientes c ON c.id = r.cliente_id
            LEFT JOIN polizas p ON p.id = r.poliza_id
            WHERE r.estado = 'PENDIENTE'
            ORDER BY r.fecha_objetivo ASC, r.id ASC
            LIMIT @limit;";

        return await cn.QueryAsync<Recordatorio>(sql, new { limit });
    }

    public async Task UpdateMensajeAsync(int id, string asunto, string mensaje)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE recordatorios
            SET asunto = @asunto,
                mensaje = @mensaje
            WHERE id = @id
              AND estado IN ('PENDIENTE', 'ERROR');";

        await cn.ExecuteAsync(sql, new { id, asunto, mensaje });
    }

    public async Task MarcarDescartadoAsync(int id)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE recordatorios
            SET estado = 'DESCARTADO'
            WHERE id = @id
              AND estado IN ('PENDIENTE', 'ERROR');";

        await cn.ExecuteAsync(sql, new { id });
    }

    public async Task MarcarEnvioAsync(int id, bool ok, string response)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            UPDATE recordatorios
            SET estado = @estado,
                fecha_envio = CASE WHEN @ok = 1 THEN NOW() ELSE fecha_envio END,
                error = @error
            WHERE id = @id;";

        await cn.ExecuteAsync(sql, new
        {
            id,
            ok,
            estado = ok ? "ENVIADO" : "ERROR",
            error = ok ? null : response
        });
    }

    public async Task<dynamic> GetStatsAsync()
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN estado = 'PENDIENTE' THEN 1 ELSE 0 END), 0) Pendientes,
                COALESCE(SUM(CASE WHEN estado = 'ENVIADO' THEN 1 ELSE 0 END), 0) Enviados,
                COALESCE(SUM(CASE WHEN estado = 'ERROR' THEN 1 ELSE 0 END), 0) Errores,
                COALESCE(SUM(CASE WHEN estado = 'DESCARTADO' THEN 1 ELSE 0 END), 0) Descartados
            FROM recordatorios;";

        return await cn.QueryFirstAsync(sql);
    }

    public async Task<IEnumerable<RecordatorioTipoResumen>> GetTiposResumenAsync(string? estado = "PENDIENTE")
    {
        using var cn = _factory.CreateConnection();

        var where = string.IsNullOrWhiteSpace(estado) ? "" : "WHERE estado = @estado";
        var sql = $@"
            SELECT tipo Tipo, COUNT(*) Total
            FROM recordatorios
            {where}
            GROUP BY tipo
            ORDER BY tipo;";

        return await cn.QueryAsync<RecordatorioTipoResumen>(sql, new { estado });
    }
}
