using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class GastoRepository
{
    private readonly DbConnectionFactory _factory;

    public GastoRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(IEnumerable<Gasto> items, int total, decimal totalRango)> GetAsync(DateTime? desde, DateTime? hasta, string? categoria, string? estado, int pagina = 1, int pageSize = 25)
    {
        using var cn = _factory.CreateConnection();
        pagina = pagina < 1 ? 1 : pagina;
        pageSize = pageSize is < 10 or > 100 ? 25 : pageSize;
        var where = new List<string> { "activo = 1" };
        var p = new DynamicParameters();

        if (desde.HasValue) { where.Add("fecha >= @desde"); p.Add("desde", desde.Value.Date); }
        if (hasta.HasValue) { where.Add("fecha <= @hasta"); p.Add("hasta", hasta.Value.Date); }
        if (!string.IsNullOrWhiteSpace(categoria)) { where.Add("categoria = @categoria"); p.Add("categoria", categoria.Trim()); }
        if (!string.IsNullOrWhiteSpace(estado)) { where.Add("estado = @estado"); p.Add("estado", estado.Trim()); }

        var whereSql = "WHERE " + string.Join(" AND ", where);
        p.Add("limit", pageSize);
        p.Add("offset", (pagina - 1) * pageSize);

        var items = await cn.QueryAsync<Gasto>($@"
            SELECT id Id, fecha Fecha, categoria Categoria, descripcion Descripcion, proveedor Proveedor, monto Monto,
                   moneda Moneda, metodo_pago MetodoPago, referencia Referencia, documento_id DocumentoId, estado Estado,
                   creado_por CreadoPor, fecha_creacion FechaCreacion, activo Activo
            FROM gastos
            {whereSql}
            ORDER BY fecha DESC, id DESC
            LIMIT @limit OFFSET @offset;", p);
        var total = await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM gastos {whereSql};", p);
        var totalRango = await cn.ExecuteScalarAsync<decimal>($"SELECT COALESCE(SUM(monto), 0) FROM gastos {whereSql};", p);
        return (items, total, totalRango);
    }

    public async Task<GastoResumen> GetResumenAsync()
    {
        using var cn = _factory.CreateConnection();
        var totalMes = await cn.ExecuteScalarAsync<decimal>(@"
            SELECT COALESCE(SUM(monto), 0)
            FROM gastos
            WHERE activo = 1
              AND fecha >= DATE_FORMAT(CURDATE(), '%Y-%m-01');");
        var porCategoria = await cn.QueryAsync<CategoriaTotal>(@"
            SELECT categoria Categoria, COALESCE(SUM(monto), 0) Total
            FROM gastos
            WHERE activo = 1
              AND fecha >= DATE_FORMAT(CURDATE(), '%Y-%m-01')
            GROUP BY categoria
            ORDER BY Total DESC;");
        return new GastoResumen { TotalMes = totalMes, PorCategoria = porCategoria };
    }

    public async Task<decimal> GetTotalMesAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<decimal>(@"
            SELECT COALESCE(SUM(monto), 0)
            FROM gastos
            WHERE activo = 1
              AND fecha >= DATE_FORMAT(CURDATE(), '%Y-%m-01');");
    }

    public async Task<int> CreateAsync(Gasto gasto, int? userId)
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO gastos (fecha, categoria, descripcion, proveedor, monto, moneda, metodo_pago, referencia, documento_id, estado, creado_por, activo)
            VALUES (@Fecha, @Categoria, @Descripcion, @Proveedor, @Monto, @Moneda, @MetodoPago, @Referencia, @DocumentoId, @Estado, @userId, 1);
            SELECT LAST_INSERT_ID();", new
        {
            gasto.Fecha,
            gasto.Categoria,
            gasto.Descripcion,
            gasto.Proveedor,
            gasto.Monto,
            gasto.Moneda,
            gasto.MetodoPago,
            gasto.Referencia,
            gasto.DocumentoId,
            gasto.Estado,
            userId
        });
    }

    public async Task<Gasto?> GetByIdAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryFirstOrDefaultAsync<Gasto>(@"
            SELECT id Id, fecha Fecha, categoria Categoria, descripcion Descripcion, proveedor Proveedor, monto Monto,
                   moneda Moneda, metodo_pago MetodoPago, referencia Referencia, documento_id DocumentoId, estado Estado,
                   creado_por CreadoPor, fecha_creacion FechaCreacion, activo Activo
            FROM gastos WHERE id = @id;", new { id });
    }

    public async Task UpdateAsync(Gasto gasto)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE gastos
            SET fecha = @Fecha, categoria = @Categoria, descripcion = @Descripcion, proveedor = @Proveedor,
                monto = @Monto, moneda = @Moneda, metodo_pago = @MetodoPago, referencia = @Referencia,
                documento_id = @DocumentoId, estado = @Estado
            WHERE id = @Id;", gasto);
    }

    public async Task SetActivoAsync(int id, bool activo)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE gastos SET activo = @activo WHERE id = @id;", new { id, activo });
    }

    public async Task DeleteAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("DELETE FROM gastos WHERE id = @id;", new { id });
    }
}
