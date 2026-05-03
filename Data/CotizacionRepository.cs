using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class CotizacionRepository
{
    private readonly DbConnectionFactory _factory;

    public CotizacionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Schema migration (called once at startup from Program.cs)
    // ────────────────────────────────────────────────────────────────────────

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizaciones (
                id              INT AUTO_INCREMENT PRIMARY KEY,
                cliente_id      INT NULL,
                cliente_nombre  VARCHAR(200) NOT NULL DEFAULT '',
                ramo            VARCHAR(100) NOT NULL DEFAULT '',
                fecha_inicio    DATE NULL,
                estado          VARCHAR(30)  NOT NULL DEFAULT 'BORRADOR',
                notas           TEXT NULL,
                creado_por      VARCHAR(100) NULL,
                fecha_creacion  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                activo          TINYINT(1) NOT NULL DEFAULT 1
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizacion_items (
                id               INT AUTO_INCREMENT PRIMARY KEY,
                cotizacion_id    INT NOT NULL,
                aseguradora      VARCHAR(100) NOT NULL DEFAULT '',
                plan             VARCHAR(200) NULL,
                prima_anual      DECIMAL(15,2) NULL,
                prima_mensual    DECIMAL(15,2) NULL,
                frecuencia_pago  VARCHAR(30)  NOT NULL DEFAULT 'MENSUAL',
                suma_asegurada   DECIMAL(15,2) NULL,
                deducible        DECIMAL(15,2) NULL,
                vigencia_meses   INT NULL,
                notas            TEXT NULL,
                ranking_puntos   DECIMAL(6,2) NULL,
                ranking_posicion INT NULL,
                recomendado      TINYINT(1) NOT NULL DEFAULT 0,
                activo           TINYINT(1) NOT NULL DEFAULT 1,
                FOREIGN KEY (cotizacion_id) REFERENCES cotizaciones(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizacion_coberturas (
                id       INT AUTO_INCREMENT PRIMARY KEY,
                item_id  INT NOT NULL,
                nombre   VARCHAR(200) NOT NULL,
                limite   VARCHAR(100) NULL,
                aplica   TINYINT(1) NOT NULL DEFAULT 1,
                FOREIGN KEY (item_id) REFERENCES cotizacion_items(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizacion_exclusiones (
                id           INT AUTO_INCREMENT PRIMARY KEY,
                item_id      INT NOT NULL,
                descripcion  TEXT NOT NULL,
                FOREIGN KEY (item_id) REFERENCES cotizacion_items(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizacion_archivos (
                id              INT AUTO_INCREMENT PRIMARY KEY,
                item_id         INT NOT NULL,
                nombre_archivo  VARCHAR(300) NOT NULL,
                ruta_archivo    VARCHAR(500) NOT NULL,
                tipo_mime       VARCHAR(100) NULL,
                extraido        TINYINT(1) NOT NULL DEFAULT 0,
                fecha_subida    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (item_id) REFERENCES cotizacion_items(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cotizacion_analisis (
                id                INT AUTO_INCREMENT PRIMARY KEY,
                cotizacion_id     INT NOT NULL,
                analisis_texto    TEXT NULL,
                ventajas_json     TEXT NULL,
                desventajas_json  TEXT NULL,
                recomendacion     TEXT NULL,
                creado_por        VARCHAR(100) NULL,
                fecha_creacion    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (cotizacion_id) REFERENCES cotizaciones(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Listado
    // ────────────────────────────────────────────────────────────────────────

    public async Task<(IEnumerable<CotizacionResumen> items, int total)> GetAsync(
        string? estado, string? buscar, int pagina = 1, int pageSize = 25)
    {
        using var cn = _factory.CreateConnection();
        pagina   = pagina < 1 ? 1 : pagina;
        pageSize = pageSize is < 5 or > 100 ? 25 : pageSize;

        var where = new List<string> { "c.activo = 1" };
        var p     = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(estado) && estado != "TODOS")
        {
            where.Add("c.estado = @estado");
            p.Add("estado", estado.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            where.Add("(c.cliente_nombre LIKE @q OR c.ramo LIKE @q OR c.estado LIKE @q)");
            p.Add("q", $"%{buscar.Trim()}%");
        }

        var sql    = "WHERE " + string.Join(" AND ", where);
        p.Add("limit",  pageSize);
        p.Add("offset", (pagina - 1) * pageSize);

        var items = await cn.QueryAsync<CotizacionResumen>($@"
            SELECT c.id Id, c.cliente_id ClienteId, c.cliente_nombre ClienteNombre,
                   c.ramo Ramo, c.fecha_inicio FechaInicio, c.estado Estado,
                   c.notas Notas, c.creado_por CreadoPor, c.fecha_creacion FechaCreacion,
                   COUNT(ci.id)         TotalItems,
                   MIN(ci.prima_anual)  MejorPrima
            FROM cotizaciones c
            LEFT JOIN cotizacion_items ci ON ci.cotizacion_id = c.id AND ci.activo = 1
            {sql}
            GROUP BY c.id
            ORDER BY c.fecha_creacion DESC
            LIMIT @limit OFFSET @offset;", p);

        var total = await cn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM cotizaciones c {sql};", p);

        return (items, total);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detalle completo
    // ────────────────────────────────────────────────────────────────────────

    public async Task<CotizacionDetalle?> GetDetalleAsync(int id)
    {
        using var cn = _factory.CreateConnection();

        var cot = await cn.QueryFirstOrDefaultAsync<Cotizacion>(@"
            SELECT id Id, cliente_id ClienteId, cliente_nombre ClienteNombre,
                   ramo Ramo, fecha_inicio FechaInicio, estado Estado,
                   notas Notas, creado_por CreadoPor, fecha_creacion FechaCreacion, activo Activo
            FROM cotizaciones WHERE id = @id AND activo = 1;", new { id });

        if (cot is null) return null;

        var items = (await cn.QueryAsync<CotizacionItem>(@"
            SELECT id Id, cotizacion_id CotizacionId, aseguradora Aseguradora, plan Plan,
                   prima_anual PrimaAnual, prima_mensual PrimaMensual, frecuencia_pago FrecuenciaPago,
                   suma_asegurada SumaAsegurada, deducible Deducible, vigencia_meses VigenciaMeses,
                   notas Notas, ranking_puntos RankingPuntos, ranking_posicion RankingPosicion,
                   recomendado Recomendado, activo Activo
            FROM cotizacion_items WHERE cotizacion_id = @id AND activo = 1
            ORDER BY ranking_posicion, id;", new { id })).ToList();

        if (items.Count > 0)
        {
            var itemIds = items.Select(i => i.Id).ToArray();

            var coberturas = await cn.QueryAsync<CotizacionCobertura>(@"
                SELECT id Id, item_id ItemId, nombre Nombre, limite Limite, aplica Aplica
                FROM cotizacion_coberturas WHERE item_id IN @ids;", new { ids = itemIds });

            var exclusiones = await cn.QueryAsync<CotizacionExclusion>(@"
                SELECT id Id, item_id ItemId, descripcion Descripcion
                FROM cotizacion_exclusiones WHERE item_id IN @ids;", new { ids = itemIds });

            var archivos = await cn.QueryAsync<CotizacionArchivo>(@"
                SELECT id Id, item_id ItemId, nombre_archivo NombreArchivo,
                       ruta_archivo RutaArchivo, tipo_mime TipoMime, extraido Extraido,
                       fecha_subida FechaSubida
                FROM cotizacion_archivos WHERE item_id IN @ids;", new { ids = itemIds });

            var cobMap  = coberturas.ToLookup(x => x.ItemId);
            var excMap  = exclusiones.ToLookup(x => x.ItemId);
            var arcMap  = archivos.ToLookup(x => x.ItemId);

            foreach (var item in items)
            {
                item.Coberturas  = cobMap[item.Id].ToList();
                item.Exclusiones = excMap[item.Id].ToList();
                item.Archivos    = arcMap[item.Id].ToList();
            }
        }

        var analisis = await cn.QueryFirstOrDefaultAsync<CotizacionAnalisis>(@"
            SELECT id Id, cotizacion_id CotizacionId, analisis_texto AnalisisTexto,
                   ventajas_json VentajasJson, desventajas_json DesventajasJson,
                   recomendacion Recomendacion, creado_por CreadoPor, fecha_creacion FechaCreacion
            FROM cotizacion_analisis WHERE cotizacion_id = @id
            ORDER BY id DESC LIMIT 1;", new { id });

        string? clienteTel = null;
        if (cot.ClienteId.HasValue)
        {
            clienteTel = await cn.ExecuteScalarAsync<string?>(
                "SELECT telefono FROM clientes WHERE id = @cid;",
                new { cid = cot.ClienteId });
        }

        return new CotizacionDetalle
        {
            Cotizacion      = cot,
            Items           = items,
            Analisis        = analisis,
            ClienteTelefono = clienteTel
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Crear / Actualizar cotización
    // ────────────────────────────────────────────────────────────────────────

    public async Task<int> CrearAsync(Cotizacion c)
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO cotizaciones
                (cliente_id, cliente_nombre, ramo, fecha_inicio, estado, notas, creado_por)
            VALUES (@ClienteId, @ClienteNombre, @Ramo, @FechaInicio, @Estado, @Notas, @CreadoPor);
            SELECT LAST_INSERT_ID();", c);
    }

    public async Task<bool> ActualizarAsync(int id, Cotizacion c)
    {
        using var cn = _factory.CreateConnection();
        var rows = await cn.ExecuteAsync(@"
            UPDATE cotizaciones
            SET cliente_id     = @ClienteId,
                cliente_nombre = @ClienteNombre,
                ramo           = @Ramo,
                fecha_inicio   = @FechaInicio,
                estado         = @Estado,
                notas          = @Notas
            WHERE id = @id AND activo = 1;",
            new { c.ClienteId, c.ClienteNombre, c.Ramo, c.FechaInicio, c.Estado, c.Notas, id });
        return rows > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = _factory.CreateConnection();
        var rows = await cn.ExecuteAsync(
            "UPDATE cotizaciones SET activo = 0 WHERE id = @id;", new { id });
        return rows > 0;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Items
    // ────────────────────────────────────────────────────────────────────────

    public async Task<int> AgregarItemAsync(CotizacionItem item)
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO cotizacion_items
                (cotizacion_id, aseguradora, plan, prima_anual, prima_mensual,
                 frecuencia_pago, suma_asegurada, deducible, vigencia_meses, notas)
            VALUES (@CotizacionId, @Aseguradora, @Plan, @PrimaAnual, @PrimaMensual,
                    @FrecuenciaPago, @SumaAsegurada, @Deducible, @VigenciaMeses, @Notas);
            SELECT LAST_INSERT_ID();", item);
    }

    public async Task<bool> ActualizarItemAsync(int itemId, CotizacionItem item)
    {
        using var cn = _factory.CreateConnection();
        var rows = await cn.ExecuteAsync(@"
            UPDATE cotizacion_items
            SET aseguradora     = @Aseguradora,
                plan            = @Plan,
                prima_anual     = @PrimaAnual,
                prima_mensual   = @PrimaMensual,
                frecuencia_pago = @FrecuenciaPago,
                suma_asegurada  = @SumaAsegurada,
                deducible       = @Deducible,
                vigencia_meses  = @VigenciaMeses,
                notas           = @Notas
            WHERE id = @itemId AND activo = 1;",
            new
            {
                item.Aseguradora, item.Plan, item.PrimaAnual, item.PrimaMensual,
                item.FrecuenciaPago, item.SumaAsegurada, item.Deducible, item.VigenciaMeses,
                item.Notas, itemId
            });
        return rows > 0;
    }

    public async Task<bool> EliminarItemAsync(int itemId)
    {
        using var cn = _factory.CreateConnection();
        var rows = await cn.ExecuteAsync(
            "UPDATE cotizacion_items SET activo = 0 WHERE id = @itemId;", new { itemId });
        return rows > 0;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Coberturas (replace-all per item)
    // ────────────────────────────────────────────────────────────────────────

    public async Task GuardarCoberturasAsync(int itemId, IEnumerable<CotizacionCobertura> coberturas)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "DELETE FROM cotizacion_coberturas WHERE item_id = @itemId;", new { itemId });

        var list = coberturas.ToList();
        if (list.Count == 0) return;

        foreach (var c in list)
        {
            await cn.ExecuteAsync(@"
                INSERT INTO cotizacion_coberturas (item_id, nombre, limite, aplica)
                VALUES (@itemId, @Nombre, @Limite, @Aplica);",
                new { itemId, c.Nombre, c.Limite, c.Aplica });
        }
    }

    public async Task GuardarExclusionesAsync(int itemId, IEnumerable<CotizacionExclusion> exclusiones)
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(
            "DELETE FROM cotizacion_exclusiones WHERE item_id = @itemId;", new { itemId });

        foreach (var e in exclusiones)
        {
            await cn.ExecuteAsync(@"
                INSERT INTO cotizacion_exclusiones (item_id, descripcion)
                VALUES (@itemId, @Descripcion);",
                new { itemId, e.Descripcion });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Ranking (calculado en backend, guardado en BD)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalculates ranking for all active items in the given cotizacion.
    /// Scoring: Price 40% | Coberturas 30% | Frecuencia pago 20% | Exclusiones 10%
    /// </summary>
    public async Task RecalcularRankingAsync(int cotizacionId)
    {
        using var cn = _factory.CreateConnection();

        var items = (await cn.QueryAsync<CotizacionItem>(@"
            SELECT id Id, prima_anual PrimaAnual, prima_mensual PrimaMensual,
                   frecuencia_pago FrecuenciaPago
            FROM cotizacion_items
            WHERE cotizacion_id = @cotizacionId AND activo = 1;",
            new { cotizacionId })).ToList();

        if (items.Count == 0) return;

        // Cargar coberturas y exclusiones para scoring
        var itemIds = items.Select(i => i.Id).ToArray();
        var cobCounts = (await cn.QueryAsync(@"
            SELECT item_id, COUNT(*) cnt FROM cotizacion_coberturas WHERE item_id IN @ids AND aplica = 1 GROUP BY item_id;",
            new { ids = itemIds })).ToDictionary(r => (int)r.item_id, r => (int)r.cnt);
        var excCounts = (await cn.QueryAsync(@"
            SELECT item_id, COUNT(*) cnt FROM cotizacion_exclusiones WHERE item_id IN @ids GROUP BY item_id;",
            new { ids = itemIds })).ToDictionary(r => (int)r.item_id, r => (int)r.cnt);

        // Normalise prima (lower = better)
        var primas = items.Select(i => i.PrimaAnual ?? i.PrimaMensual * 12 ?? 0m).ToList();
        var minPrima = primas.Any(p => p > 0) ? primas.Where(p => p > 0).Min() : 1m;
        var maxPrima = primas.Max();
        var primaRange = maxPrima - minPrima;

        // Frecuencia score (more options / flexibility = better)
        decimal FrecScore(string f) => f switch
        {
            "MENSUAL"    => 1.0m,
            "TRIMESTRAL" => 0.8m,
            "SEMESTRAL"  => 0.6m,
            "ANUAL"      => 0.5m,
            _            => 0.5m
        };

        var maxCob = cobCounts.Values.Any() ? cobCounts.Values.Max() : 1;
        var maxExc = excCounts.Values.Any() ? excCounts.Values.Max() : 1;

        var scored = items.Select(item =>
        {
            var prima = item.PrimaAnual ?? item.PrimaMensual * 12 ?? 0m;
            var precioScore = primaRange > 0 && prima > 0
                ? 1m - (prima - minPrima) / primaRange
                : 1m;
            var cobScore  = maxCob > 0 ? (decimal)(cobCounts.GetValueOrDefault(item.Id, 0)) / maxCob : 0m;
            var frecScore = FrecScore(item.FrecuenciaPago);
            var excScore  = maxExc > 0 ? 1m - (decimal)(excCounts.GetValueOrDefault(item.Id, 0)) / maxExc : 1m;

            var total = precioScore * 0.40m
                      + cobScore   * 0.30m
                      + frecScore  * 0.20m
                      + excScore   * 0.10m;

            return (item.Id, Puntos: Math.Round(total * 100, 2));
        }).OrderByDescending(x => x.Puntos).ToList();

        for (var i = 0; i < scored.Count; i++)
        {
            await cn.ExecuteAsync(@"
                UPDATE cotizacion_items
                SET ranking_puntos   = @pts,
                    ranking_posicion = @pos,
                    recomendado      = @rec
                WHERE id = @id;",
                new { pts = scored[i].Puntos, pos = i + 1, rec = i == 0, id = scored[i].Id });
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Análisis
    // ────────────────────────────────────────────────────────────────────────

    public async Task<int> GuardarAnalisisAsync(CotizacionAnalisis analisis)
    {
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>(@"
            INSERT INTO cotizacion_analisis
                (cotizacion_id, analisis_texto, ventajas_json, desventajas_json, recomendacion, creado_por)
            VALUES (@CotizacionId, @AnalisisTexto, @VentajasJson, @DesventajasJson, @Recomendacion, @CreadoPor);
            SELECT LAST_INSERT_ID();", analisis);
    }

    public async Task<bool> ActualizarAnalisisAsync(int analisisId, CotizacionAnalisis analisis)
    {
        using var cn = _factory.CreateConnection();
        var rows = await cn.ExecuteAsync(@"
            UPDATE cotizacion_analisis
            SET analisis_texto   = @AnalisisTexto,
                ventajas_json    = @VentajasJson,
                desventajas_json = @DesventajasJson,
                recomendacion    = @Recomendacion
            WHERE id = @analisisId;",
            new
            {
                analisis.AnalisisTexto, analisis.VentajasJson,
                analisis.DesventajasJson, analisis.Recomendacion,
                analisisId
            });
        return rows > 0;
    }
}
