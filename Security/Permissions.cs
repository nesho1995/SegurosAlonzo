namespace ReclamosWhatsApp.Security;

public static class Permissions
{
    public const string DashboardVer = "dashboard.ver";
    public const string ClientesVer = "clientes.ver";
    public const string ClientesEditar = "clientes.editar";
    public const string ClientesCrear = "clientes.crear";
    public const string PolizasVer = "polizas.ver";
    public const string PolizasEditar = "polizas.editar";
    public const string PolizasCrear = "polizas.crear";
    public const string PagosVer = "pagos.ver";
    public const string PagosEditar = "pagos.editar";
    public const string ReclamosVer = "reclamos.ver";
    public const string ReclamosEditar = "reclamos.editar";
    public const string ReclamosEnviar = "reclamos.enviar";
    public const string RecordatoriosVer = "recordatorios.ver";
    public const string RecordatoriosEnviar = "recordatorios.enviar";
    public const string TalleresVer = "talleres.ver";
    public const string TalleresEditar = "talleres.editar";
    public const string DocumentosVer = "documentos.ver";
    public const string DocumentosSubir = "documentos.subir";
    public const string DocumentosEliminar = "documentos.eliminar";
    public const string AuditoriaVer = "auditoria.ver";
    public const string UsuariosAdministrar = "usuarios.administrar";
    public const string ConfiguracionAdministrar = "configuracion.administrar";
    public const string CatalogosAdministrar = "catalogos.administrar";
    public const string AutomatizacionesVer = "automatizaciones.ver";
    public const string AutomatizacionesCrear = "automatizaciones.crear";
    public const string AutomatizacionesEditar = "automatizaciones.editar";
    public const string AutomatizacionesEliminar = "automatizaciones.eliminar";
    public const string GastosVer = "gastos.ver";
    public const string GastosCrear = "gastos.crear";
    public const string GastosEditar = "gastos.editar";
    public const string GastosEliminar = "gastos.eliminar";

    public const string CotizacionesVer = "cotizaciones.ver";
    public const string CotizacionesCrear = "cotizaciones.crear";
    public const string CotizacionesEditar = "cotizaciones.editar";
    public const string CotizacionesEliminar = "cotizaciones.eliminar";

    public static readonly string[] All =
    {
        DashboardVer, ClientesVer, ClientesEditar, ClientesCrear, PolizasVer, PolizasEditar, PolizasCrear,
        PagosVer, PagosEditar, ReclamosVer, ReclamosEditar, ReclamosEnviar, RecordatoriosVer, RecordatoriosEnviar,
        TalleresVer, TalleresEditar, DocumentosVer, DocumentosSubir, DocumentosEliminar, AuditoriaVer,
        UsuariosAdministrar, ConfiguracionAdministrar, CatalogosAdministrar, AutomatizacionesVer, AutomatizacionesCrear,
        AutomatizacionesEditar, AutomatizacionesEliminar, GastosVer, GastosCrear, GastosEditar, GastosEliminar,
        CotizacionesVer, CotizacionesCrear, CotizacionesEditar, CotizacionesEliminar
    };

    private static readonly Dictionary<string, string[]> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADMIN"] = All,
        ["GERENTE"] = new[]
        {
            DashboardVer, ClientesVer, ClientesEditar, ClientesCrear, PolizasVer, PolizasEditar, PolizasCrear,
            PagosVer, PagosEditar, ReclamosVer, ReclamosEditar, ReclamosEnviar, RecordatoriosVer, RecordatoriosEnviar,
            TalleresVer, TalleresEditar, DocumentosVer, DocumentosSubir, DocumentosEliminar, AuditoriaVer,
            AutomatizacionesVer, AutomatizacionesCrear, AutomatizacionesEditar, AutomatizacionesEliminar,
            GastosVer, GastosCrear, GastosEditar, GastosEliminar,
            CotizacionesVer, CotizacionesCrear, CotizacionesEditar, CotizacionesEliminar
        },
        ["EJECUTIVO"] = new[]
        {
            DashboardVer, ClientesVer, ClientesEditar, ClientesCrear, PolizasVer, PolizasEditar, PolizasCrear,
            PagosVer, ReclamosVer, ReclamosEditar, RecordatoriosVer, TalleresVer, DocumentosVer, DocumentosSubir,
            AutomatizacionesVer, GastosVer, GastosCrear, GastosEditar,
            CotizacionesVer, CotizacionesCrear, CotizacionesEditar
        },
        ["SOLO_LECTURA"] = new[]
        {
            DashboardVer, ClientesVer, PolizasVer, PagosVer, ReclamosVer, RecordatoriosVer, TalleresVer, DocumentosVer,
            GastosVer, CotizacionesVer
        }
    };

    public static IReadOnlyCollection<string> ForRole(string? role)
    {
        if (role is not null && RolePermissions.TryGetValue(role, out var permissions))
            return permissions;

        return RolePermissions["SOLO_LECTURA"];
    }

    public static string NormalizeRole(string? role)
    {
        return (role ?? "").Trim().ToUpperInvariant() switch
        {
            "ADMIN" => "ADMIN",
            "ADMINISTRADOR" => "ADMIN",
            "GERENTE" => "GERENTE",
            "EJECUTIVO" => "EJECUTIVO",
            "USER" => "EJECUTIVO",
            "SOLO_LECTURA" => "SOLO_LECTURA",
            "SOLO LECTURA" => "SOLO_LECTURA",
            _ => "SOLO_LECTURA"
        };
    }
}
