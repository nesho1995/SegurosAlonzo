using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class ReclamoPatronesRepository
{
    private readonly DbConnectionFactory _factory;

    public ReclamoPatronesRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync()
    {
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS correo_reclamo_patrones (
                id INT AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(120) NOT NULL,
                activo TINYINT(1) NOT NULL DEFAULT 1,
                prioridad INT NOT NULL DEFAULT 100,
                campo_destino VARCHAR(60) NOT NULL,
                fuente VARCHAR(20) NOT NULL DEFAULT 'SUBJECT_BODY',
                tipo_regla VARCHAR(20) NOT NULL DEFAULT 'REGEX',
                patron TEXT NOT NULL,
                grupo_regex VARCHAR(80) NULL,
                requerido TINYINT(1) NOT NULL DEFAULT 0,
                normalizar_texto TINYINT(1) NOT NULL DEFAULT 1,
                descripcion TEXT NULL,
                ejemplo_entrada TEXT NULL,
                ejemplo_salida_esperada TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS correo_reclamo_plantillas (
                id INT AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(120) NOT NULL,
                activa TINYINT(1) NOT NULL DEFAULT 1,
                prioridad INT NOT NULL DEFAULT 100,
                descripcion TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS correo_reclamo_plantilla_reglas (
                plantilla_id INT NOT NULL,
                patron_id INT NOT NULL,
                PRIMARY KEY (plantilla_id, patron_id)
            );
            CREATE TABLE IF NOT EXISTS correo_reclamo_condiciones (
                id INT AUTO_INCREMENT PRIMARY KEY,
                plantilla_id INT NOT NULL,
                fuente VARCHAR(20) NOT NULL DEFAULT 'SUBJECT_BODY',
                tipo_regla VARCHAR(20) NOT NULL DEFAULT 'CONTIENE',
                patron TEXT NOT NULL,
                operador_grupo VARCHAR(10) NOT NULL DEFAULT 'AND',
                grupo_condicion INT NOT NULL DEFAULT 1
            );");
    }

    public async Task<IEnumerable<CorreoReclamoPatron>> GetPatronesAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<CorreoReclamoPatron>(@"
            SELECT id Id, nombre Nombre, activo Activo, prioridad Prioridad, campo_destino CampoDestino, fuente Fuente,
                   tipo_regla TipoRegla, patron Patron, grupo_regex GrupoRegex, requerido Requerido, normalizar_texto NormalizarTexto,
                   descripcion Descripcion, ejemplo_entrada EjemploEntrada, ejemplo_salida_esperada EjemploSalidaEsperada
            FROM correo_reclamo_patrones
            ORDER BY prioridad ASC, id ASC;");
    }

    public async Task<int> UpsertPatronAsync(CorreoReclamoPatron model)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        if (model.Id <= 0)
        {
            return await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO correo_reclamo_patrones
                (nombre, activo, prioridad, campo_destino, fuente, tipo_regla, patron, grupo_regex, requerido, normalizar_texto, descripcion, ejemplo_entrada, ejemplo_salida_esperada)
                VALUES
                (@Nombre, @Activo, @Prioridad, @CampoDestino, @Fuente, @TipoRegla, @Patron, @GrupoRegex, @Requerido, @NormalizarTexto, @Descripcion, @EjemploEntrada, @EjemploSalidaEsperada);
                SELECT LAST_INSERT_ID();", model);
        }

        await cn.ExecuteAsync(@"
            UPDATE correo_reclamo_patrones
            SET nombre = @Nombre,
                activo = @Activo,
                prioridad = @Prioridad,
                campo_destino = @CampoDestino,
                fuente = @Fuente,
                tipo_regla = @TipoRegla,
                patron = @Patron,
                grupo_regex = @GrupoRegex,
                requerido = @Requerido,
                normalizar_texto = @NormalizarTexto,
                descripcion = @Descripcion,
                ejemplo_entrada = @EjemploEntrada,
                ejemplo_salida_esperada = @EjemploSalidaEsperada
            WHERE id = @Id;", model);
        return model.Id;
    }

    public async Task<IEnumerable<CorreoReclamoPlantilla>> GetPlantillasAsync()
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<CorreoReclamoPlantilla>(@"
            SELECT id Id, nombre Nombre, activa Activa, prioridad Prioridad, descripcion Descripcion
            FROM correo_reclamo_plantillas
            ORDER BY prioridad ASC, id ASC;");
    }

    public async Task<int> UpsertPlantillaAsync(CorreoReclamoPlantilla model)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        if (model.Id <= 0)
        {
            return await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO correo_reclamo_plantillas (nombre, activa, prioridad, descripcion)
                VALUES (@Nombre, @Activa, @Prioridad, @Descripcion);
                SELECT LAST_INSERT_ID();", model);
        }

        await cn.ExecuteAsync(@"
            UPDATE correo_reclamo_plantillas
            SET nombre = @Nombre,
                activa = @Activa,
                prioridad = @Prioridad,
                descripcion = @Descripcion
            WHERE id = @Id;", model);
        return model.Id;
    }

    public async Task SetPlantillaReglasAsync(int plantillaId, IEnumerable<int> patronIds)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        await cn.ExecuteAsync("DELETE FROM correo_reclamo_plantilla_reglas WHERE plantilla_id = @plantillaId;", new { plantillaId });
        foreach (var patronId in patronIds.Distinct())
            await cn.ExecuteAsync("INSERT INTO correo_reclamo_plantilla_reglas (plantilla_id, patron_id) VALUES (@plantillaId, @patronId);", new { plantillaId, patronId });
    }

    public async Task<IEnumerable<CorreoReclamoPlantillaRegla>> GetPlantillaReglasAsync(int plantillaId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<CorreoReclamoPlantillaRegla>("SELECT plantilla_id PlantillaId, patron_id PatronId FROM correo_reclamo_plantilla_reglas WHERE plantilla_id = @plantillaId;", new { plantillaId });
    }

    public async Task<IEnumerable<CorreoReclamoCondicion>> GetCondicionesAsync(int plantillaId)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        return await cn.QueryAsync<CorreoReclamoCondicion>(@"
            SELECT id Id, plantilla_id PlantillaId, fuente Fuente, tipo_regla TipoRegla, patron Patron, operador_grupo OperadorGrupo, grupo_condicion GrupoCondicion
            FROM correo_reclamo_condiciones
            WHERE plantilla_id = @plantillaId
            ORDER BY grupo_condicion ASC, id ASC;", new { plantillaId });
    }

    public async Task<int> UpsertCondicionAsync(CorreoReclamoCondicion model)
    {
        await EnsureSchemaAsync();
        using var cn = _factory.CreateConnection();
        if (model.Id <= 0)
        {
            return await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO correo_reclamo_condiciones (plantilla_id, fuente, tipo_regla, patron, operador_grupo, grupo_condicion)
                VALUES (@PlantillaId, @Fuente, @TipoRegla, @Patron, @OperadorGrupo, @GrupoCondicion);
                SELECT LAST_INSERT_ID();", model);
        }

        await cn.ExecuteAsync(@"
            UPDATE correo_reclamo_condiciones
            SET fuente = @Fuente,
                tipo_regla = @TipoRegla,
                patron = @Patron,
                operador_grupo = @OperadorGrupo,
                grupo_condicion = @GrupoCondicion
            WHERE id = @Id;", model);
        return model.Id;
    }
}
