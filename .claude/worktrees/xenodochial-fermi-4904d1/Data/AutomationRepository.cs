using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class AutomationRepository
{
    private readonly DbConnectionFactory _factory;

    public AutomationRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Automatizacion>> GetAllAsync()
    {
        using var cn = _factory.CreateConnection();
        var rules = (await cn.QueryAsync<Automatizacion>(@"
            SELECT id Id, nombre Nombre, activo Activo, tipo_evento TipoEvento, empresa_id EmpresaId, fecha_creacion FechaCreacion
            FROM automatizaciones
            ORDER BY fecha_creacion DESC, id DESC;")).ToList();

        if (rules.Count == 0)
            return rules;

        var ids = rules.Select(x => x.Id).ToArray();
        var condiciones = (await cn.QueryAsync<AutomatizacionCondicion>(@"
            SELECT id Id, automatizacion_id AutomatizacionId, campo Campo, operador Operador, valor Valor
            FROM automatizacion_condiciones
            WHERE automatizacion_id IN @ids
            ORDER BY id;", new { ids })).ToList();
        var acciones = (await cn.QueryAsync<AutomatizacionAccion>(@"
            SELECT id Id, automatizacion_id AutomatizacionId, tipo_accion TipoAccion, parametros_json ParametrosJson
            FROM automatizacion_acciones
            WHERE automatizacion_id IN @ids
            ORDER BY id;", new { ids })).ToList();

        foreach (var rule in rules)
        {
            rule.Condiciones = condiciones.Where(x => x.AutomatizacionId == rule.Id).ToList();
            rule.Acciones = acciones.Where(x => x.AutomatizacionId == rule.Id).ToList();
        }

        return rules;
    }

    public async Task<IEnumerable<Automatizacion>> GetActiveByEventAsync(string tipoEvento, int? empresaId = null)
    {
        using var cn = _factory.CreateConnection();
        var rules = (await cn.QueryAsync<Automatizacion>(@"
            SELECT id Id, nombre Nombre, activo Activo, tipo_evento TipoEvento, empresa_id EmpresaId, fecha_creacion FechaCreacion
            FROM automatizaciones
            WHERE activo = 1
              AND tipo_evento = @tipoEvento
              AND (empresa_id IS NULL OR empresa_id = @empresaId)
            ORDER BY id;", new { tipoEvento, empresaId })).ToList();

        if (rules.Count == 0)
            return rules;

        var ids = rules.Select(x => x.Id).ToArray();
        var condiciones = (await cn.QueryAsync<AutomatizacionCondicion>(@"
            SELECT id Id, automatizacion_id AutomatizacionId, campo Campo, operador Operador, valor Valor
            FROM automatizacion_condiciones
            WHERE automatizacion_id IN @ids
            ORDER BY id;", new { ids })).ToList();
        var acciones = (await cn.QueryAsync<AutomatizacionAccion>(@"
            SELECT id Id, automatizacion_id AutomatizacionId, tipo_accion TipoAccion, parametros_json ParametrosJson
            FROM automatizacion_acciones
            WHERE automatizacion_id IN @ids
            ORDER BY id;", new { ids })).ToList();

        foreach (var rule in rules)
        {
            rule.Condiciones = condiciones.Where(x => x.AutomatizacionId == rule.Id).ToList();
            rule.Acciones = acciones.Where(x => x.AutomatizacionId == rule.Id).ToList();
        }

        return rules;
    }

    public async Task<int> InsertAsync(AutomatizacionRequest request)
    {
        using var cn = _factory.CreateConnection();
        cn.Open();
        using var tx = cn.BeginTransaction();

        var id = await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO automatizaciones (nombre, activo, tipo_evento, empresa_id)
            VALUES (@Nombre, @Activo, @TipoEvento, @EmpresaId);
            SELECT LAST_INSERT_ID();", new
        {
            Nombre = request.Nombre.Trim(),
            request.Activo,
            TipoEvento = NormalizeEvent(request.TipoEvento),
            request.EmpresaId
        }, tx);

        await ReplaceChildrenAsync(cn, tx, id, request.Condiciones, request.Acciones);
        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, AutomatizacionRequest request)
    {
        using var cn = _factory.CreateConnection();
        cn.Open();
        using var tx = cn.BeginTransaction();

        var affected = await cn.ExecuteAsync(@"
            UPDATE automatizaciones
            SET nombre = @Nombre,
                activo = @Activo,
                tipo_evento = @TipoEvento,
                empresa_id = @EmpresaId
            WHERE id = @Id;", new
        {
            Id = id,
            Nombre = request.Nombre.Trim(),
            request.Activo,
            TipoEvento = NormalizeEvent(request.TipoEvento),
            request.EmpresaId
        }, tx);

        if (affected == 0)
        {
            tx.Rollback();
            return false;
        }

        await cn.ExecuteAsync("DELETE FROM automatizacion_condiciones WHERE automatizacion_id = @id;", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM automatizacion_acciones WHERE automatizacion_id = @id;", new { id }, tx);
        await ReplaceChildrenAsync(cn, tx, id, request.Condiciones, request.Acciones);

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        var affected = await cn.ExecuteAsync("DELETE FROM automatizaciones WHERE id = @id;", new { id });
        return affected > 0;
    }

    public async Task InsertLogAsync(AutomatizacionLog log)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            INSERT INTO automatizacion_logs (automatizacion_id, entidad_tipo, entidad_id, resultado, mensaje)
            VALUES (@AutomatizacionId, @EntidadTipo, @EntidadId, @Resultado, @Mensaje);", log);
    }

    public async Task<bool> HasSuccessfulExecutionAsync(int automatizacionId, string entidadTipo, int? entidadId)
    {
        if (!entidadId.HasValue)
            return false;

        using var cn = _factory.CreateConnection();
        var count = await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM automatizacion_logs
            WHERE automatizacion_id = @automatizacionId
              AND entidad_tipo = @entidadTipo
              AND entidad_id = @entidadId
              AND resultado IN ('EJECUTADA', 'PREPARADA');", new
        {
            automatizacionId,
            entidadTipo = NormalizeEntity(entidadTipo),
            entidadId
        });

        return count > 0;
    }

    public async Task<IEnumerable<AutomatizacionLog>> GetLogsAsync(int? automatizacionId = null)
    {
        using var cn = _factory.CreateConnection();
        var where = automatizacionId.HasValue ? "WHERE l.automatizacion_id = @automatizacionId" : "";
        return await cn.QueryAsync<AutomatizacionLog>($@"
            SELECT
                l.id Id,
                l.automatizacion_id AutomatizacionId,
                a.nombre Automatizacion,
                l.entidad_tipo EntidadTipo,
                l.entidad_id EntidadId,
                l.resultado Resultado,
                l.mensaje Mensaje,
                l.fecha Fecha
            FROM automatizacion_logs l
            INNER JOIN automatizaciones a ON a.id = l.automatizacion_id
            {where}
            ORDER BY l.fecha DESC
            LIMIT 100;", new { automatizacionId });
    }

    public async Task<int> CountErroresAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        using var cn = _factory.CreateConnection();
        var where = new List<string> { "resultado = 'ERROR'" };
        var p = new DynamicParameters();
        if (desde.HasValue)
        {
            where.Add("fecha >= @desde");
            p.Add("desde", desde.Value.Date);
        }
        if (hasta.HasValue)
        {
            where.Add("fecha < @hasta");
            p.Add("hasta", hasta.Value.Date.AddDays(1));
        }

        return await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM automatizacion_logs WHERE {string.Join(" AND ", where)};", p);
    }

    private static async Task ReplaceChildrenAsync(
        System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,
        int id,
        IEnumerable<AutomatizacionCondicionRequest> condiciones,
        IEnumerable<AutomatizacionAccionRequest> acciones)
    {
        foreach (var item in condiciones.Where(x => !string.IsNullOrWhiteSpace(x.Campo)))
        {
            await cn.ExecuteAsync(@"
                INSERT INTO automatizacion_condiciones (automatizacion_id, campo, operador, valor)
                VALUES (@AutomatizacionId, @Campo, @Operador, @Valor);", new
            {
                AutomatizacionId = id,
                Campo = NormalizeField(item.Campo),
                Operador = NormalizeOperator(item.Operador),
                Valor = item.Valor?.Trim()
            }, tx);
        }

        foreach (var item in acciones.Where(x => !string.IsNullOrWhiteSpace(x.TipoAccion)))
        {
            await cn.ExecuteAsync(@"
                INSERT INTO automatizacion_acciones (automatizacion_id, tipo_accion, parametros_json)
                VALUES (@AutomatizacionId, @TipoAccion, @ParametrosJson);", new
            {
                AutomatizacionId = id,
                TipoAccion = NormalizeAction(item.TipoAccion),
                item.ParametrosJson
            }, tx);
        }
    }

    private static string NormalizeEvent(string value) => (value ?? "").Trim().ToLowerInvariant();
    private static string NormalizeField(string value) => (value ?? "").Trim();
    private static string NormalizeOperator(string value) => (value ?? "=").Trim().ToLowerInvariant();
    private static string NormalizeAction(string value) => (value ?? "").Trim().ToLowerInvariant();
    private static string NormalizeEntity(string value) => (value ?? "").Trim().ToUpperInvariant();
}
