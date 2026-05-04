using Dapper;
using ReclamosWhatsApp.Models;
using System.Text.Json;

namespace ReclamosWhatsApp.Data;

public class ComparativoRepository
{
    private readonly DbConnectionFactory _factory;
    public ComparativoRepository(DbConnectionFactory factory) => _factory = factory;

    // ─── Schema ─────────────────────────────────────────────────────────────

    public async Task EnsureSchemaAsync()
    {
        using var c = _factory.CreateConnection();
        await c.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS comparativos (
                id          INT AUTO_INCREMENT PRIMARY KEY,
                usuario_id  INT NOT NULL DEFAULT 0,
                cliente     VARCHAR(200) NOT NULL,
                vehiculo    VARCHAR(200),
                notas       TEXT,
                estado      VARCHAR(20) NOT NULL DEFAULT 'borrador',
                creado_en   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS comparativo_items (
                id                          INT AUTO_INCREMENT PRIMARY KEY,
                comparativo_id              INT NOT NULL,
                aseguradora                 VARCHAR(150) NOT NULL,
                nombre_archivo              VARCHAR(255),
                texto_extraido              MEDIUMTEXT,
                prima_anual                 DECIMAL(12,2),
                prima_mensual               DECIMAL(12,2),
                prima_contado               DECIMAL(12,2),
                descuento_contado           DECIMAL(10,4),
                descuento_es_porcentaje     TINYINT(1) NOT NULL DEFAULT 1,
                recargo_financiamiento      DECIMAL(10,4),
                recargo_es_porcentaje       TINYINT(1) NOT NULL DEFAULT 1,
                prima_financiada            DECIMAL(12,2),
                forma_pago                  VARCHAR(60),
                suma_asegurada              DECIMAL(14,2),
                deducible_colision          DECIMAL(10,4),
                deducible_colision_pct      TINYINT(1) NOT NULL DEFAULT 1,
                deducible_robo              DECIMAL(10,4),
                deducible_robo_pct          TINYINT(1) NOT NULL DEFAULT 1,
                vigencia_desde              VARCHAR(20),
                vigencia_hasta              VARCHAR(20),
                coberturas_json             TEXT,
                exclusiones_json            TEXT,
                score                       DECIMAL(5,2),
                posicion                    INT,
                creado_en                   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (comparativo_id) REFERENCES comparativos(id) ON DELETE CASCADE
            );
        ");
    }

    // ─── CRUD comparativos ──────────────────────────────────────────────────

    public async Task<ComparativoListResponse> GetAsync(int page, int pageSize, string? q)
    {
        using var c = _factory.CreateConnection();
        page     = page < 1 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 20 : pageSize;

        var where = string.IsNullOrWhiteSpace(q)
            ? "" : " WHERE (cliente LIKE @q OR vehiculo LIKE @q)";
        var qParam = $"%{q}%";

        var total = await c.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM comparativos{where}", new { q = qParam });

        var items = (await c.QueryAsync<Comparativo>(
            $"SELECT * FROM comparativos{where} ORDER BY creado_en DESC LIMIT @pageSize OFFSET @offset",
            new { q = qParam, pageSize, offset = (page - 1) * pageSize })).ToList();

        return new ComparativoListResponse { Items = items, Total = total };
    }

    public async Task<ComparativoDetalle?> GetDetalleAsync(int id)
    {
        using var c = _factory.CreateConnection();
        var comp = await c.QueryFirstOrDefaultAsync<Comparativo>(
            "SELECT * FROM comparativos WHERE id = @id", new { id });
        if (comp == null) return null;

        var rawItems = (await c.QueryAsync<ComparativoItem>(
            "SELECT * FROM comparativo_items WHERE comparativo_id = @id ORDER BY posicion, id",
            new { id })).ToList();

        var detalle = rawItems.Select(i =>
        {
            var d = MapDetalle(i);
            d.AhorroContado = (d.PrimaFinanciada.HasValue && d.PrimaContado.HasValue)
                ? d.PrimaFinanciada.Value - d.PrimaContado.Value : null;
            return d;
        }).ToList();

        comp.Items = rawItems;
        return new ComparativoDetalle { Comparativo = comp, Items = detalle };
    }

    public async Task<int> CrearAsync(int usuarioId, string cliente, string? vehiculo, string? notas)
    {
        using var c = _factory.CreateConnection();
        return await c.ExecuteScalarAsync<int>(
            @"INSERT INTO comparativos (usuario_id, cliente, vehiculo, notas)
              VALUES (@usuarioId, @cliente, @vehiculo, @notas);
              SELECT LAST_INSERT_ID();",
            new { usuarioId, cliente, vehiculo, notas });
    }

    public async Task ActualizarAsync(int id, string cliente, string? vehiculo, string? notas, string? estado)
    {
        using var c = _factory.CreateConnection();
        await c.ExecuteAsync(
            @"UPDATE comparativos SET cliente=@cliente, vehiculo=@vehiculo, notas=@notas,
              estado=COALESCE(@estado,estado) WHERE id=@id",
            new { id, cliente, vehiculo, notas, estado });
    }

    public async Task EliminarAsync(int id)
    {
        using var c = _factory.CreateConnection();
        await c.ExecuteAsync("DELETE FROM comparativos WHERE id=@id", new { id });
    }

    // ─── Items ───────────────────────────────────────────────────────────────

    public async Task<int> AgregarItemAsync(ComparativoItem item)
    {
        using var c = _factory.CreateConnection();
        return await c.ExecuteScalarAsync<int>(@"
            INSERT INTO comparativo_items
              (comparativo_id, aseguradora, nombre_archivo, texto_extraido,
               prima_anual, prima_mensual, prima_contado,
               descuento_contado, descuento_es_porcentaje,
               recargo_financiamiento, recargo_es_porcentaje, prima_financiada,
               forma_pago, suma_asegurada,
               deducible_colision, deducible_colision_pct,
               deducible_robo, deducible_robo_pct,
               vigencia_desde, vigencia_hasta,
               coberturas_json, exclusiones_json)
            VALUES
              (@ComparativoId, @Aseguradora, @NombreArchivo, @TextoExtraido,
               @PrimaAnual, @PrimaMensual, @PrimaContado,
               @DescuentoContado, @DescuentoEsPorcentaje,
               @RecargoFinanciamiento, @RecargoEsPorcentaje, @PrimaFinanciada,
               @FormaPago, @SumaAsegurada,
               @DeducibleColision, @DeducibleColisionEsPorcentaje,
               @DeducibleRobo, @DeducibleRoboEsPorcentaje,
               @VigenciaDesde, @VigenciaHasta,
               @CoberturasJson, @ExclusionesJson);
            SELECT LAST_INSERT_ID();", item);
    }

    public async Task ActualizarItemAsync(ComparativoItem item)
    {
        using var c = _factory.CreateConnection();
        await c.ExecuteAsync(@"
            UPDATE comparativo_items SET
              aseguradora=@Aseguradora,
              prima_anual=@PrimaAnual, prima_mensual=@PrimaMensual,
              prima_contado=@PrimaContado,
              descuento_contado=@DescuentoContado, descuento_es_porcentaje=@DescuentoEsPorcentaje,
              recargo_financiamiento=@RecargoFinanciamiento, recargo_es_porcentaje=@RecargoEsPorcentaje,
              prima_financiada=@PrimaFinanciada, forma_pago=@FormaPago,
              suma_asegurada=@SumaAsegurada,
              deducible_colision=@DeducibleColision, deducible_colision_pct=@DeducibleColisionEsPorcentaje,
              deducible_robo=@DeducibleRobo, deducible_robo_pct=@DeducibleRoboEsPorcentaje,
              vigencia_desde=@VigenciaDesde, vigencia_hasta=@VigenciaHasta,
              coberturas_json=@CoberturasJson, exclusiones_json=@ExclusionesJson
            WHERE id=@Id", item);
    }

    public async Task EliminarItemAsync(int itemId)
    {
        using var c = _factory.CreateConnection();
        await c.ExecuteAsync("DELETE FROM comparativo_items WHERE id=@itemId", new { itemId });
    }

    // ─── Ranking ─────────────────────────────────────────────────────────────

    public async Task RecalcularRankingAsync(int comparativoId)
    {
        using var c = _factory.CreateConnection();
        var items = (await c.QueryAsync<ComparativoItem>(
            "SELECT * FROM comparativo_items WHERE comparativo_id = @comparativoId",
            new { comparativoId })).ToList();

        if (items.Count == 0) return;

        var primaVals = items.Select(i => i.PrimaContado ?? i.PrimaAnual ?? 0).ToList();
        var primaMin  = primaVals.Where(v => v > 0).DefaultIfEmpty(1).Min();
        var primaMax  = primaVals.DefaultIfEmpty(1).Max();
        var dedVals   = items.Select(i => i.DeducibleColision ?? 0).ToList();
        var dedMin    = dedVals.Where(v => v > 0).DefaultIfEmpty(0).Min();
        var dedMax    = dedVals.DefaultIfEmpty(1).Max();
        var cobCounts = items.Select(i => (double)Deser(i.CoberturasJson).Count).ToList();
        var cobMax    = cobCounts.DefaultIfEmpty(1).Max();

        for (int idx = 0; idx < items.Count; idx++)
        {
            var item  = items[idx];
            var prima = primaVals[idx];
            var ded   = dedVals[idx];
            var cobN  = cobCounts[idx];

            double sp = prima > 0 && primaMax != primaMin
                ? 1.0 - (double)(prima - primaMin) / (double)(primaMax - primaMin) : 0.5;
            double sd = ded > 0 && dedMax != dedMin
                ? 1.0 - (double)(ded - dedMin) / (double)(dedMax - dedMin) : 0.5;
            double sc = cobMax > 0 ? cobN / cobMax : 0;

            double sr = 0.5;
            if (item.DeducibleRobo.HasValue)
            {
                var robVals = items.Select(i => i.DeducibleRobo ?? 0).ToList();
                var robMin  = robVals.Where(v => v > 0).DefaultIfEmpty(0).Min();
                var robMax  = robVals.DefaultIfEmpty(1).Max();
                sr = robMax != robMin
                    ? 1.0 - (double)((item.DeducibleRobo ?? 0) - robMin) / (double)(robMax - robMin) : 0.5;
            }

            item.Score = (decimal)Math.Round(sp * 40 + sc * 30 + sd * 20 + sr * 10, 1);
        }

        var ordered = items.OrderByDescending(i => i.Score).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Posicion = i + 1;

        foreach (var item in items)
            await c.ExecuteAsync(
                "UPDATE comparativo_items SET score=@Score, posicion=@Posicion WHERE id=@Id",
                new { item.Score, item.Posicion, item.Id });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    static ComparativoItemDetalle MapDetalle(ComparativoItem i) => new()
    {
        Id = i.Id, ComparativoId = i.ComparativoId, Aseguradora = i.Aseguradora,
        NombreArchivo = i.NombreArchivo, TextoExtraido = i.TextoExtraido,
        PrimaAnual = i.PrimaAnual, PrimaMensual = i.PrimaMensual,
        PrimaContado = i.PrimaContado, DescuentoContado = i.DescuentoContado,
        DescuentoEsPorcentaje = i.DescuentoEsPorcentaje,
        RecargoFinanciamiento = i.RecargoFinanciamiento, RecargoEsPorcentaje = i.RecargoEsPorcentaje,
        PrimaFinanciada = i.PrimaFinanciada, FormaPago = i.FormaPago,
        SumaAsegurada = i.SumaAsegurada,
        DeducibleColision = i.DeducibleColision, DeducibleColisionEsPorcentaje = i.DeducibleColisionEsPorcentaje,
        DeducibleRobo = i.DeducibleRobo, DeducibleRoboEsPorcentaje = i.DeducibleRoboEsPorcentaje,
        VigenciaDesde = i.VigenciaDesde, VigenciaHasta = i.VigenciaHasta,
        CoberturasJson = i.CoberturasJson, ExclusionesJson = i.ExclusionesJson,
        Score = i.Score, Posicion = i.Posicion, CreadoEn = i.CreadoEn,
        Coberturas  = Deser(i.CoberturasJson),
        Exclusiones = Deser(i.ExclusionesJson),
    };

    static List<string> Deser(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
