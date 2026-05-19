using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class ReporteReclamosRepository
{
    private readonly DbConnectionFactory _factory;

    public ReporteReclamosRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(IEnumerable<ReporteReclamoItem> items, int total)> GetReclamosAsync(ReporteReclamosFiltro filtro)
    {
        using var cn = _factory.CreateConnection();
        filtro.PageSize = filtro.PageSize is < 20 or > 500 ? 200 : filtro.PageSize;

        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filtro.Estado) && filtro.Estado != "TODOS")
        {
            where.Add("COALESCE(r.estado_reclamo, r.estado) = @estado");
            p.Add("estado", filtro.Estado.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ciudad))
        {
            where.Add("COALESCE(r.ciudad_detectada, '') LIKE @ciudad");
            p.Add("ciudad", $"%{filtro.Ciudad.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            where.Add(@"(
                COALESCE(r.reclamo, '') LIKE @buscar
                OR COALESCE(r.numero_reclamo, '') LIKE @buscar
                OR COALESCE(r.poliza, '') LIKE @buscar
                OR COALESCE(r.placa, '') LIKE @buscar
                OR COALESCE(r.conductor, '') LIKE @buscar
                OR COALESCE(r.asegurado, '') LIKE @buscar
                OR COALESCE(r.celular, '') LIKE @buscar
            )");
            p.Add("buscar", $"%{filtro.Buscar.Trim()}%");
        }

        if (filtro.Desde.HasValue)
        {
            p.Add("desde", filtro.Desde.Value.Date);
        }

        if (filtro.Hasta.HasValue)
        {
            p.Add("hasta", filtro.Hasta.Value.Date.AddDays(1));
        }

        if (filtro.SoloConMovimiento)
        {
            var movementWhere = new List<string>();
            if (filtro.Desde.HasValue)
                movementWhere.Add("a.fecha >= @desde");
            if (filtro.Hasta.HasValue)
                movementWhere.Add("a.fecha < @hasta");
            var movementSql = movementWhere.Count > 0 ? " AND " + string.Join(" AND ", movementWhere) : "";
            where.Add($@"EXISTS (
                SELECT 1
                FROM auditoria_logs a
                WHERE a.entidad_tipo = 'RECLAMO'
                  AND a.entidad_id = r.id
                  {movementSql}
            )");
        }

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        p.Add("limit", filtro.PageSize);

        var eventRange = BuildEventRangeSql(filtro);
        var sql = $@"
            SELECT
                r.id Id,
                r.reclamo Reclamo,
                r.poliza Poliza,
                r.placa Placa,
                r.conductor Conductor,
                r.celular Celular,
                r.asegurado Asegurado,
                COALESCE(r.estado_reclamo, r.estado) Estado,
                r.ciudad_detectada CiudadDetectada,
                r.descripcion Descripcion,
                r.fecha_creacion FechaCreacion,
                r.fecha_ultimo_recordatorio FechaUltimoRecordatorio,
                COALESCE(r.cantidad_recordatorios, 0) CantidadRecordatorios,
                COALESCE(docs.pendientes, 0) DocumentosPendientes,
                COALESCE(docs.recibidos, 0) DocumentosRecibidos,
                COALESCE(audit.eventos_periodo, 0) EventosPeriodo,
                last_a.fecha UltimoMovimientoFecha,
                last_a.accion UltimoMovimientoAccion,
                last_a.descripcion UltimoMovimientoDescripcion,
                COALESCE(u.Username, 'Sistema') UltimoMovimientoUsuario
            FROM reclamos_whatsapp r
            LEFT JOIN (
                SELECT reclamo_id,
                       SUM(CASE WHEN recibido = 0 THEN 1 ELSE 0 END) pendientes,
                       SUM(CASE WHEN recibido = 1 THEN 1 ELSE 0 END) recibidos
                FROM reclamo_documentos
                GROUP BY reclamo_id
            ) docs ON docs.reclamo_id = r.id
            LEFT JOIN (
                SELECT entidad_id, COUNT(*) eventos_periodo
                FROM auditoria_logs
                WHERE entidad_tipo = 'RECLAMO'
                  {eventRange}
                GROUP BY entidad_id
            ) audit ON audit.entidad_id = r.id
            LEFT JOIN auditoria_logs last_a ON last_a.id = (
                SELECT a2.id
                FROM auditoria_logs a2
                WHERE a2.entidad_tipo = 'RECLAMO'
                  AND a2.entidad_id = r.id
                ORDER BY a2.fecha DESC, a2.id DESC
                LIMIT 1
            )
            LEFT JOIN Users u ON u.Id = last_a.usuario_id
            {whereSql}
            ORDER BY COALESCE(last_a.fecha, r.fecha_creacion) DESC, r.id DESC
            LIMIT @limit;";

        var countSql = $@"
            SELECT COUNT(*)
            FROM reclamos_whatsapp r
            {whereSql};";

        var items = await cn.QueryAsync<ReporteReclamoItem>(sql, p);
        var total = await cn.ExecuteScalarAsync<int>(countSql, p);
        return (items, total);
    }

    private static string BuildEventRangeSql(ReporteReclamosFiltro filtro)
    {
        var parts = new List<string>();
        if (filtro.Desde.HasValue)
            parts.Add("fecha >= @desde");
        if (filtro.Hasta.HasValue)
            parts.Add("fecha < @hasta");
        return parts.Count == 0 ? "" : " AND " + string.Join(" AND ", parts);
    }
}
