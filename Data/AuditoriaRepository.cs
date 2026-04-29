using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class AuditoriaRepository
{
    private readonly DbConnectionFactory _factory;

    public AuditoriaRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task InsertAsync(AuditoriaLog log)
    {
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO auditoria_logs
            (usuario_id, accion, entidad_tipo, entidad_id, descripcion, ip)
            VALUES
            (@UsuarioId, @Accion, @EntidadTipo, @EntidadId, @Descripcion, @Ip);";

        await cn.ExecuteAsync(sql, log);
    }

    public async Task<(IEnumerable<AuditoriaLog> items, int total)> GetAsync(AuditoriaFiltro filtro)
    {
        using var cn = _factory.CreateConnection();
        filtro.Pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        filtro.PageSize = filtro.PageSize is < 10 or > 200 ? 50 : filtro.PageSize;

        var where = new List<string>();
        var p = new DynamicParameters();

        if (filtro.UsuarioId.HasValue)
        {
            where.Add("a.usuario_id = @usuarioId");
            p.Add("usuarioId", filtro.UsuarioId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            where.Add("a.entidad_tipo = @tipo");
            p.Add("tipo", filtro.Tipo.Trim().ToUpperInvariant());
        }

        if (filtro.Desde.HasValue)
        {
            where.Add("a.fecha >= @desde");
            p.Add("desde", filtro.Desde.Value.Date);
        }

        if (filtro.Hasta.HasValue)
        {
            where.Add("a.fecha < @hasta");
            p.Add("hasta", filtro.Hasta.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            where.Add("(a.accion LIKE @buscar OR a.descripcion LIKE @buscar OR a.entidad_tipo LIKE @buscar OR u.Username LIKE @buscar)");
            p.Add("buscar", $"%{filtro.Buscar.Trim()}%");
        }

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        p.Add("limit", filtro.PageSize);
        p.Add("offset", (filtro.Pagina - 1) * filtro.PageSize);

        var sql = $@"
            SELECT
                a.id Id,
                a.usuario_id UsuarioId,
                COALESCE(u.Username, 'Sistema') Usuario,
                a.accion Accion,
                a.entidad_tipo EntidadTipo,
                a.entidad_id EntidadId,
                a.descripcion Descripcion,
                a.fecha Fecha,
                a.ip Ip
            FROM auditoria_logs a
            LEFT JOIN Users u ON u.Id = a.usuario_id
            {whereSql}
            ORDER BY a.fecha DESC
            LIMIT @limit OFFSET @offset;";

        var countSql = $@"
            SELECT COUNT(*)
            FROM auditoria_logs a
            LEFT JOIN Users u ON u.Id = a.usuario_id
            {whereSql};";

        var items = await cn.QueryAsync<AuditoriaLog>(sql, p);
        var total = await cn.ExecuteScalarAsync<int>(countSql, p);
        return (items, total);
    }
}
