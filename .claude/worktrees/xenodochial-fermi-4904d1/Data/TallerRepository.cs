using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class TallerRepository
{
    private readonly DbConnectionFactory _factory;

    public TallerRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Taller>> GetAsync(
        string? buscar = null,
        string? estado = "ACTIVO",
        string? ciudad = null,
        string? aseguradora = null)
    {
        await EnsureSchemaAsync();
        await SeedDefaultTegucigalpaAsync();
        using var cn = _factory.CreateConnection();
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            where.Add("(t.nombre LIKE @buscar OR t.ciudad LIKE @buscar OR t.zona LIKE @buscar OR t.direccion LIKE @buscar OR ta.aseguradora LIKE @buscar OR tr.ramo_normalizado LIKE @buscar)");
            p.Add("buscar", $"%{buscar.Trim()}%");
        }

        if (estado == "ACTIVO")
            where.Add("t.activo = 1");
        else if (estado == "INACTIVO")
            where.Add("t.activo = 0");

        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            where.Add("t.ciudad LIKE @ciudad");
            p.Add("ciudad", $"%{Normalize(ciudad)}%");
        }

        if (!string.IsNullOrWhiteSpace(aseguradora))
        {
            where.Add("ta.aseguradora = @aseguradora");
            p.Add("aseguradora", Normalize(aseguradora));
        }

        var sql = $@"
            SELECT DISTINCT
                t.id Id, t.nombre Nombre, t.ciudad Ciudad, t.zona Zona, t.direccion Direccion,
                t.telefono Telefono, t.whatsapp WhatsApp, t.email Email, t.contacto Contacto,
                t.aseguradora Aseguradora,
                t.ramo Ramo,
                t.activo Activo, t.es_preferido EsPreferido, t.orden_prioridad OrdenPrioridad,
                t.observaciones Observaciones, t.fecha_creacion FechaCreacion
            FROM talleres t
            LEFT JOIN taller_aseguradoras ta ON ta.taller_id = t.id AND ta.activo = 1
            LEFT JOIN taller_ramos tr ON tr.taller_id = t.id AND tr.activo = 1
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY t.ciudad, t.es_preferido DESC, t.orden_prioridad, t.nombre
            LIMIT 300;";

        var items = (await cn.QueryAsync<Taller>(sql, p)).ToList();
        await HydrateRelationsAsync(cn, items);
        return items;
    }

    public async Task<int> InsertAsync(Taller taller)
    {
        await EnsureSchemaAsync();
        NormalizeModel(taller);
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO talleres (nombre, ciudad, zona, direccion, telefono, whatsapp, email, contacto, aseguradora, ramo, activo, es_preferido, orden_prioridad, observaciones)
            VALUES (@Nombre, @Ciudad, @Zona, @Direccion, @Telefono, @WhatsApp, @Email, @Contacto, @Aseguradora, @Ramo, @Activo, @EsPreferido, @OrdenPrioridad, @Observaciones);
            SELECT LAST_INSERT_ID();";

        var id = await cn.ExecuteScalarAsync<int>(sql, taller);
        await SaveRelationsAsync(cn, id, taller);
        return id;
    }

    public async Task UpsertAsync(Taller taller)
    {
        await EnsureSchemaAsync();
        NormalizeModel(taller);
        using var cn = _factory.CreateConnection();
        var existing = await cn.ExecuteScalarAsync<int?>(@"
            SELECT id FROM talleres WHERE nombre = @Nombre AND ciudad = @Ciudad LIMIT 1;",
            new { taller.Nombre, taller.Ciudad });

        if (existing.HasValue)
        {
            taller.Id = existing.Value;
            await UpdateAsync(taller);
        }
        else
        {
            await InsertAsync(taller);
        }
    }

    public async Task UpdateAsync(Taller taller)
    {
        await EnsureSchemaAsync();
        NormalizeModel(taller);
        using var cn = _factory.CreateConnection();
        const string sql = @"
            UPDATE talleres
            SET nombre = @Nombre,
                ciudad = @Ciudad,
                zona = @Zona,
                direccion = @Direccion,
                telefono = @Telefono,
                whatsapp = @WhatsApp,
                email = @Email,
                contacto = @Contacto,
                aseguradora = @Aseguradora,
                ramo = @Ramo,
                activo = @Activo,
                es_preferido = @EsPreferido,
                orden_prioridad = @OrdenPrioridad,
                observaciones = @Observaciones
            WHERE id = @Id;";

        await cn.ExecuteAsync(sql, taller);
        await SaveRelationsAsync(cn, taller.Id, taller);
    }

    public async Task SetActivoAsync(int id, bool activo)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE talleres SET activo = @activo WHERE id = @id;", new { id, activo });
    }

    public async Task<IEnumerable<TallerSugerido>> SugerirAsync(string? ciudad, string? aseguradora, string? ramo)
    {
        await EnsureSchemaAsync();
        await SeedDefaultTegucigalpaAsync();
        var ciudadNorm = Normalize(ciudad);
        var aseguradoraNorm = Normalize(aseguradora);
        var ramoNorm = NormalizeRamo(ramo);
        using var cn = _factory.CreateConnection();

        var rows = await QuerySuggestionsAsync(cn, ciudadNorm, aseguradoraNorm, ramoNorm, requireAseguradora: true, requireRamo: true, criterio: "Ciudad, aseguradora y ramo");
        if (rows.Count == 0)
            rows = await QuerySuggestionsAsync(cn, ciudadNorm, aseguradoraNorm, "", requireAseguradora: true, requireRamo: false, criterio: "Ciudad y aseguradora");
        if (rows.Count == 0)
            rows = await QuerySuggestionsAsync(cn, ciudadNorm, "", "", requireAseguradora: false, requireRamo: false, criterio: "Ciudad");

        return rows;
    }

    private static async Task<List<TallerSugerido>> QuerySuggestionsAsync(
        System.Data.IDbConnection cn,
        string ciudad,
        string aseguradora,
        string ramo,
        bool requireAseguradora,
        bool requireRamo,
        string criterio)
    {
        if (string.IsNullOrWhiteSpace(ciudad))
            return [];

        var sql = $@"
            SELECT DISTINCT
                t.id Id, t.nombre Nombre, t.ciudad Ciudad, COALESCE(ta.aseguradora, t.aseguradora, '') Aseguradora,
                COALESCE(tr.ramo_normalizado, t.ramo, '') Ramo, t.telefono Telefono, t.whatsapp WhatsApp,
                t.direccion Direccion, t.es_preferido EsPreferido, t.orden_prioridad OrdenPrioridad,
                @criterio Criterio
            FROM talleres t
            LEFT JOIN taller_aseguradoras ta ON ta.taller_id = t.id AND ta.activo = 1
            LEFT JOIN taller_ramos tr ON tr.taller_id = t.id AND tr.activo = 1
            WHERE t.activo = 1
              AND t.ciudad = @ciudad
              {(requireAseguradora ? "AND ta.aseguradora = @aseguradora" : "")}
              {(requireRamo ? "AND tr.ramo_normalizado = @ramo" : "")}
            ORDER BY t.es_preferido DESC, t.orden_prioridad ASC, t.nombre ASC
            LIMIT 5;";

        return (await cn.QueryAsync<TallerSugerido>(sql, new { ciudad, aseguradora, ramo, criterio })).ToList();
    }

    public async Task<IEnumerable<TallerDetectado>> GetDetectadosAsync(string estado = "PENDIENTE")
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            SELECT id Id, nombre Nombre, ciudad Ciudad, aseguradora Aseguradora, ramo Ramo,
                   telefono Telefono, direccion Direccion, texto_origen TextoOrigen,
                   estado Estado, fecha_creacion FechaCreacion
            FROM talleres_detectados
            WHERE estado = @estado
            ORDER BY fecha_creacion DESC
            LIMIT 200;";

        return await cn.QueryAsync<TallerDetectado>(sql, new { estado });
    }

    public async Task<int> CountDetectadosPendientesAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM talleres_detectados WHERE estado = 'PENDIENTE';");
    }

    public async Task<int> InsertDetectadoAsync(TallerDetectado item)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO talleres_detectados
            (nombre, ciudad, aseguradora, ramo, telefono, direccion, texto_origen, estado)
            VALUES
            (@Nombre, @Ciudad, @Aseguradora, @Ramo, @Telefono, @Direccion, @TextoOrigen, @Estado);
            SELECT LAST_INSERT_ID();";

        return await cn.ExecuteScalarAsync<int>(sql, item);
    }

    public async Task AprobarDetectadoAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        var item = await cn.QueryFirstOrDefaultAsync<TallerDetectado>(@"
            SELECT id Id, nombre Nombre, ciudad Ciudad, aseguradora Aseguradora, ramo Ramo,
                   telefono Telefono, direccion Direccion, texto_origen TextoOrigen,
                   estado Estado, fecha_creacion FechaCreacion
            FROM talleres_detectados
            WHERE id = @id;", new { id });

        if (item is null)
            return;

        await InsertAsync(new Taller
        {
            Nombre = item.Nombre,
            Ciudad = item.Ciudad ?? "",
            Aseguradora = item.Aseguradora ?? "",
            Ramo = item.Ramo,
            AseguradorasAceptadas = SplitMulti(item.Aseguradora),
            RamosAtendidos = SplitMulti(item.Ramo).Select(NormalizeRamo).ToList(),
            Telefono = item.Telefono,
            Direccion = item.Direccion,
            Activo = true
        });

        await cn.ExecuteAsync("UPDATE talleres_detectados SET estado = 'APROBADO' WHERE id = @id;", new { id });
    }

    public async Task DescartarDetectadoAsync(int id)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("UPDATE talleres_detectados SET estado = 'DESCARTADO' WHERE id = @id;", new { id });
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS talleres (
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(200) NOT NULL,
                ciudad VARCHAR(120) NOT NULL,
                aseguradora VARCHAR(160) NOT NULL DEFAULT '',
                ramo VARCHAR(120) NULL,
                telefono VARCHAR(60) NULL,
                direccion VARCHAR(500) NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX ix_talleres_busqueda (ciudad, aseguradora, ramo),
                INDEX ix_talleres_activo (activo)
            );

            ALTER TABLE talleres
                ADD COLUMN IF NOT EXISTS zona VARCHAR(120) NULL,
                ADD COLUMN IF NOT EXISTS direccion VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS telefono VARCHAR(60) NULL,
                ADD COLUMN IF NOT EXISTS whatsapp VARCHAR(60) NULL,
                ADD COLUMN IF NOT EXISTS email VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS contacto VARCHAR(180) NULL,
                ADD COLUMN IF NOT EXISTS es_preferido TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS orden_prioridad INT NOT NULL DEFAULT 100,
                ADD COLUMN IF NOT EXISTS observaciones TEXT NULL;

            CREATE TABLE IF NOT EXISTS taller_aseguradoras (
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                taller_id INT NOT NULL,
                aseguradora VARCHAR(160) NOT NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                UNIQUE KEY uq_taller_aseguradora (taller_id, aseguradora),
                INDEX ix_taller_aseguradora (aseguradora)
            );

            CREATE TABLE IF NOT EXISTS taller_ramos (
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                taller_id INT NOT NULL,
                ramo_normalizado VARCHAR(120) NOT NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                UNIQUE KEY uq_taller_ramo (taller_id, ramo_normalizado),
                INDEX ix_taller_ramo (ramo_normalizado)
            );

            CREATE TABLE IF NOT EXISTS talleres_detectados (
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(200) NOT NULL,
                ciudad VARCHAR(120) NULL,
                aseguradora VARCHAR(160) NULL,
                ramo VARCHAR(120) NULL,
                telefono VARCHAR(60) NULL,
                direccion VARCHAR(500) NULL,
                texto_origen TEXT NOT NULL,
                estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
                fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX ix_talleres_detectados_estado (estado)
            );");
    }

    public async Task SeedDefaultTegucigalpaAsync()
    {
        using var cn = _factory.CreateConnection();
        var talleres = new[]
        {
            new Taller { Nombre = "Taller Auto Excel Tegucigalpa", Ciudad = "TEGUCIGALPA", Zona = "MORAZAN", Direccion = "Blvd. Morazan, Tegucigalpa", Telefono = "2239-0001", WhatsApp = "50499990001", AseguradorasAceptadas = ["CREFISA", "GENERAL"], RamosAtendidos = ["AUTOS"], EsPreferido = true, OrdenPrioridad = 1, Activo = true },
            new Taller { Nombre = "Taller El Prado", Ciudad = "TEGUCIGALPA", Zona = "EL PRADO", Direccion = "Colonia El Prado, Tegucigalpa", Telefono = "2235-0002", WhatsApp = "50499990002", AseguradorasAceptadas = ["CREFISA", "GENERAL"], RamosAtendidos = ["AUTOS", "MOTOS"], OrdenPrioridad = 2, Activo = true },
            new Taller { Nombre = "Pintura y Enderezado La Kennedy", Ciudad = "TEGUCIGALPA", Zona = "KENNEDY", Direccion = "Colonia Kennedy, Tegucigalpa", Telefono = "2240-0003", WhatsApp = "50499990003", AseguradorasAceptadas = ["GENERAL"], RamosAtendidos = ["AUTOS"], OrdenPrioridad = 3, Activo = true },
            new Taller { Nombre = "Taller Metropolis", Ciudad = "TEGUCIGALPA", Zona = "ANILLO PERIFERICO", Direccion = "Anillo Periferico, Tegucigalpa", Telefono = "2216-0004", WhatsApp = "50499990004", AseguradorasAceptadas = ["GENERAL"], RamosAtendidos = ["AUTOS"], OrdenPrioridad = 4, Activo = true }
        };

        foreach (var taller in talleres)
            await UpsertSeedAsync(cn, taller);
    }

    private static async Task UpsertSeedAsync(System.Data.IDbConnection cn, Taller taller)
    {
        NormalizeModel(taller);
        var id = await cn.ExecuteScalarAsync<int?>(@"SELECT id FROM talleres WHERE nombre = @Nombre AND ciudad = @Ciudad LIMIT 1;", new { taller.Nombre, taller.Ciudad });
        if (!id.HasValue)
        {
            id = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO talleres (nombre, ciudad, zona, direccion, telefono, whatsapp, email, contacto, aseguradora, ramo, activo, es_preferido, orden_prioridad, observaciones)
                VALUES (@Nombre, @Ciudad, @Zona, @Direccion, @Telefono, @WhatsApp, @Email, @Contacto, @Aseguradora, @Ramo, @Activo, @EsPreferido, @OrdenPrioridad, @Observaciones);
                SELECT LAST_INSERT_ID();", taller);
        }

        await SaveRelationsAsync(cn, id.Value, taller);
    }

    private static async Task SaveRelationsAsync(System.Data.IDbConnection cn, int tallerId, Taller taller)
    {
        var aseguradoras = taller.AseguradorasAceptadas.Count > 0 ? taller.AseguradorasAceptadas : SplitMulti(taller.Aseguradora);
        var ramos = taller.RamosAtendidos.Count > 0 ? taller.RamosAtendidos : SplitMulti(taller.Ramo);

        await cn.ExecuteAsync("UPDATE taller_aseguradoras SET activo = 0 WHERE taller_id = @tallerId;", new { tallerId });
        await cn.ExecuteAsync("UPDATE taller_ramos SET activo = 0 WHERE taller_id = @tallerId;", new { tallerId });

        foreach (var aseg in aseguradoras.Select(Normalize).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await cn.ExecuteAsync(@"
                INSERT INTO taller_aseguradoras (taller_id, aseguradora, activo)
                VALUES (@tallerId, @aseguradora, 1)
                ON DUPLICATE KEY UPDATE activo = 1;", new { tallerId, aseguradora = aseg });
        }

        foreach (var ramo in ramos.Select(NormalizeRamo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await cn.ExecuteAsync(@"
                INSERT INTO taller_ramos (taller_id, ramo_normalizado, activo)
                VALUES (@tallerId, @ramo, 1)
                ON DUPLICATE KEY UPDATE activo = 1;", new { tallerId, ramo });
        }
    }

    private static async Task HydrateRelationsAsync(System.Data.IDbConnection cn, List<Taller> talleres)
    {
        if (talleres.Count == 0)
            return;

        var ids = talleres.Select(x => x.Id).ToArray();
        var aseguradoras = await cn.QueryAsync<(int TallerId, string Aseguradora)>("SELECT taller_id TallerId, aseguradora Aseguradora FROM taller_aseguradoras WHERE activo = 1 AND taller_id IN @ids;", new { ids });
        var ramos = await cn.QueryAsync<(int TallerId, string Ramo)>("SELECT taller_id TallerId, ramo_normalizado Ramo FROM taller_ramos WHERE activo = 1 AND taller_id IN @ids;", new { ids });

        foreach (var taller in talleres)
        {
            taller.AseguradorasAceptadas = aseguradoras.Where(x => x.TallerId == taller.Id).Select(x => x.Aseguradora).ToList();
            taller.RamosAtendidos = ramos.Where(x => x.TallerId == taller.Id).Select(x => x.Ramo).ToList();
        }
    }

    private static void NormalizeModel(Taller taller)
    {
        taller.Nombre = (taller.Nombre ?? "").Trim();
        taller.Ciudad = Normalize(taller.Ciudad);
        taller.Zona = NormalizeNullable(taller.Zona);
        taller.AseguradorasAceptadas = (taller.AseguradorasAceptadas.Count > 0 ? taller.AseguradorasAceptadas : SplitMulti(taller.Aseguradora)).Select(Normalize).Where(x => x != "").Distinct().ToList();
        taller.RamosAtendidos = (taller.RamosAtendidos.Count > 0 ? taller.RamosAtendidos : SplitMulti(taller.Ramo)).Select(NormalizeRamo).Where(x => x != "").Distinct().ToList();
        taller.Aseguradora = taller.AseguradorasAceptadas.FirstOrDefault() ?? "";
        taller.Ramo = taller.RamosAtendidos.FirstOrDefault();
        taller.OrdenPrioridad = taller.OrdenPrioridad <= 0 ? 100 : taller.OrdenPrioridad;
    }

    public static string Normalize(string? value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }

    public static string NormalizeRamo(string? value)
    {
        var text = Normalize(value);
        return text switch
        {
            "AUTO" or "AUTOS" or "VEHICULO" or "VEHICULOS" => "AUTOS",
            "MOTO" or "MOTOS" => "MOTOS",
            _ => text
        };
    }

    private static string? NormalizeNullable(string? value)
    {
        var text = Normalize(value);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static List<string> SplitMulti(string? value)
    {
        return (value ?? "")
            .Replace(",", ";")
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}
