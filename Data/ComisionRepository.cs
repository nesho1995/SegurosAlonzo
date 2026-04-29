using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class ComisionRepository
{
    private readonly DbConnectionFactory _factory;

    public ComisionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> CreateLoteAsync(string? aseguradora, string archivoNombre, int? userId)
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO comisiones_lotes (aseguradora, archivo_nombre, usuario_id, estado)
            VALUES (@aseguradora, @archivoNombre, @userId, 'EN_REVISION');
            SELECT LAST_INSERT_ID();", new { aseguradora, archivoNombre, userId });
    }

    public async Task InsertDetalleAsync(ComisionDetalle item)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            INSERT INTO comisiones_detalle
            (lote_id, poliza_id, cliente_detectado, poliza_detectada, aseguradora_detectada, prima_detectada,
             porcentaje_detectado, comision_detectada, comision_esperada, diferencia, fecha_pago, referencia, estado, observaciones)
            VALUES
            (@LoteId, @PolizaId, @ClienteDetectado, @PolizaDetectada, @AseguradoraDetectada, @PrimaDetectada,
             @PorcentajeDetectado, @ComisionDetectada, @ComisionEsperada, @Diferencia, @FechaPago, @Referencia, @Estado, @Observaciones);", item);
    }

    public async Task<(IEnumerable<ComisionLote> lotes, IEnumerable<ComisionDetalle> detalles)> GetAsync(int? loteId = null)
    {
        using var cn = _factory.CreateConnection();
        var lotes = await cn.QueryAsync<ComisionLote>(@"
            SELECT id Id, aseguradora Aseguradora, archivo_nombre ArchivoNombre, fecha_carga FechaCarga, usuario_id UsuarioId, estado Estado
            FROM comisiones_lotes
            ORDER BY fecha_carga DESC
            LIMIT 50;");
        var selected = loteId ?? lotes.FirstOrDefault()?.Id;
        var detalles = selected.HasValue
            ? await cn.QueryAsync<ComisionDetalle>(@"
                SELECT id Id, lote_id LoteId, poliza_id PolizaId, cliente_detectado ClienteDetectado,
                       poliza_detectada PolizaDetectada, aseguradora_detectada AseguradoraDetectada,
                       prima_detectada PrimaDetectada, porcentaje_detectado PorcentajeDetectado,
                       comision_detectada ComisionDetectada, comision_esperada ComisionEsperada, diferencia Diferencia,
                       fecha_pago FechaPago, referencia Referencia, estado Estado, observaciones Observaciones, revisado Revisado
                FROM comisiones_detalle
                WHERE lote_id = @selected
                ORDER BY FIELD(estado, 'REQUIERE_REVISION', 'DIFERENCIA_MONTO', 'POLIZA_NO_ENCONTRADA', 'DUPLICADO', 'COINCIDE'), id;", new { selected })
            : [];
        return (lotes, detalles);
    }

    public async Task<int> CountPendientesAsync()
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM comisiones_detalle WHERE revisado = 0 AND estado <> 'COINCIDE';");
    }

    public async Task<dynamic?> FindPolicyAsync(string? aseguradora, string? numeroPoliza, string? cliente)
    {
        using var cn = _factory.CreateConnection();
        return await cn.QueryFirstOrDefaultAsync(@"
            SELECT p.id Id, p.prima_total PrimaTotal, c.nombre Cliente, p.aseguradora Aseguradora, p.numero_poliza NumeroPoliza
            FROM polizas p
            INNER JOIN clientes c ON c.id = p.cliente_id
            WHERE (@numeroPoliza IS NULL OR @numeroPoliza = '' OR p.numero_poliza = @numeroPoliza)
              AND (@aseguradora IS NULL OR @aseguradora = '' OR p.aseguradora LIKE CONCAT('%', @aseguradora, '%'))
            ORDER BY
                CASE WHEN c.nombre = @cliente THEN 0 WHEN c.nombre LIKE CONCAT('%', @cliente, '%') THEN 1 ELSE 2 END,
                p.id DESC
            LIMIT 1;", new { aseguradora, numeroPoliza, cliente });
    }

    public async Task MarcarRevisadoAsync(int id, int? userId)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            UPDATE comisiones_detalle
            SET revisado = 1, fecha_revision = NOW(), usuario_revision_id = @userId
            WHERE id = @id;", new { id, userId });
    }
}
