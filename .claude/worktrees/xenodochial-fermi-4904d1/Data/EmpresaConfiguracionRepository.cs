using Dapper;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Data;

public class EmpresaConfiguracionRepository
{
    private readonly DbConnectionFactory _factory;

    public EmpresaConfiguracionRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<EmpresaConfiguracion> GetAsync()
    {
        using var cn = _factory.CreateConnection();
        await EnsureSchemaAsync(cn);
        var config = await cn.QueryFirstOrDefaultAsync<EmpresaConfiguracion>(@"
            SELECT id Id, nombre_empresa NombreEmpresa, telefono_empresa TelefonoEmpresa,
                   logo_ruta LogoRuta, color_primario ColorPrimario,
                   fecha_actualizacion FechaActualizacion, usuario_actualizacion_id UsuarioActualizacionId
            FROM empresa_configuracion
            WHERE id = 1;");

        if (config is not null)
            return config;

        await cn.ExecuteAsync("INSERT INTO empresa_configuracion (id, nombre_empresa, color_primario) VALUES (1, 'Seguros Alonzo', '#2563eb');");
        return await GetAsync();
    }

    public async Task UpdateAsync(EmpresaConfiguracion config, int? userId)
    {
        using var cn = _factory.CreateConnection();
        await EnsureSchemaAsync(cn);
        await cn.ExecuteAsync(@"
            UPDATE empresa_configuracion
            SET nombre_empresa = @NombreEmpresa,
                telefono_empresa = @TelefonoEmpresa,
                color_primario = @ColorPrimario,
                usuario_actualizacion_id = @userId
            WHERE id = 1;", new { config.NombreEmpresa, config.TelefonoEmpresa, config.ColorPrimario, userId });
    }

    public async Task UpdateLogoAsync(string logoRuta, int? userId)
    {
        using var cn = _factory.CreateConnection();
        await EnsureSchemaAsync(cn);
        await cn.ExecuteAsync(@"
            UPDATE empresa_configuracion
            SET logo_ruta = @logoRuta,
                usuario_actualizacion_id = @userId
            WHERE id = 1;", new { logoRuta, userId });
    }

    private static Task EnsureSchemaAsync(System.Data.IDbConnection cn)
    {
        return cn.ExecuteAsync(@"
            ALTER TABLE empresa_configuracion
                ADD COLUMN IF NOT EXISTS telefono_empresa VARCHAR(60) NULL;");
    }
}
