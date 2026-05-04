using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class MessageBuilderService
{
    private readonly TallerRepository _talleres;
    private readonly EmpresaConfiguracionRepository _empresa;

    public MessageBuilderService(TallerRepository talleres, EmpresaConfiguracionRepository empresa)
    {
        _talleres = talleres;
        _empresa = empresa;
    }

    public string GenerateMessage(ReclamoWhatsApp r)
    {
        return GenerateMessage(r, []);
    }

    public async Task<string> GenerateMessageAsync(ReclamoWhatsApp r)
    {
        var ciudad = DetectarCiudad(r.LugarAccidente);
        var talleres = (await _talleres.SugerirAsync(ciudad, r.Aseguradora, r.TipoReclamo ?? "AUTOS")).ToList();
        var empresa = await _empresa.GetAsync();

        r.CiudadDetectada = ciudad;
        r.TallerSugeridoId = talleres.FirstOrDefault()?.Id;
        r.TallerAsignadoId ??= r.TallerSugeridoId;
        r.MotivoSugerenciaTaller = talleres.Count == 0
            ? "No hay taller parametrizado"
            : talleres.First().Criterio;

        return GenerateMessage(r, talleres, empresa.TelefonoEmpresa);
    }

    private static string GenerateMessage(ReclamoWhatsApp r, IEnumerable<TallerSugerido> talleresSugeridos, string? telefonoEmpresa = null)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var fecha = r.FechaNotificacion?.ToString("dd/MM/yyyy") ?? "";
        var lugar = string.IsNullOrWhiteSpace(r.LugarAccidente) ? "el lugar indicado en el reclamo" : r.LugarAccidente;
        var talleres = talleresSugeridos.Take(5).ToList();
        var bloqueTalleres = talleres.Count == 0
            ? $@"
Para coordinacion de taller o inspeccion, puede contactarse con la aseguradora o con nosotros para indicarle el proceso correspondiente.{(string.IsNullOrWhiteSpace(telefonoEmpresa) ? "" : $"{Environment.NewLine}Telefono empresa: {telefonoEmpresa}")}"
            : $@"
Talleres en red:
{string.Join(Environment.NewLine, talleres.Select(x => $"- {x.Nombre}: {x.Direccion}{(string.IsNullOrWhiteSpace(x.Telefono) ? "" : $" / Tel. {x.Telefono}")}"))}";

        return $@"
Buenas tardes, {nombre}.

Reciba un cordial saludo. Le comunicamos que su reclamo fue notificado con fecha {fecha}, ocurrido en {lugar}.

Para poder avanzar con la gestion debe completar la siguiente documentacion:

1. Aviso de accidente original firmado por el conductor y asegurado. Si es empresa, aplicar sello correspondiente.
2. Certificacion de Transito.
3. Tarjeta de identidad del conductor, ambos lados.
4. Licencia del conductor, ambos lados.
5. Boleta de circulacion del vehiculo asegurado.
6. Inspeccion puntual de danos en Seguros Crefisa.
7. Dos cotizaciones de talleres de la red, cuando aplique.
8. Estar al dia con el pago de primas del seguro.

{bloqueTalleres}

Una vez se entregue completa la informacion, se evaluara la cobertura y la aplicacion de deducibles.

Atentamente.".Trim();
    }

    private static string? DetectarCiudad(string? lugar)
    {
        if (string.IsNullOrWhiteSpace(lugar))
            return null;

        if (lugar.Contains("TEGUCIGALPA", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("TGU", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("FRANCISCO MORAZAN", StringComparison.OrdinalIgnoreCase))
            return "TEGUCIGALPA";

        if (lugar.Contains("SAN PEDRO SULA", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("S.P.S", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("CORTES", StringComparison.OrdinalIgnoreCase))
            return "SAN PEDRO SULA";

        return lugar.Trim();
    }
}
