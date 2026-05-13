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
        var ciudad = HondurasLocationService.DetectCity(r.LugarAccidente);
        var talleres = HondurasLocationService.IsTegucigalpa(ciudad)
            ? new List<TallerSugerido>()
            : (await _talleres.SugerirAsync(ciudad, r.Aseguradora, r.TipoReclamo ?? "AUTOS")).ToList();
        var empresa = await _empresa.GetAsync();

        r.CiudadDetectada = ciudad;
        r.TallerSugeridoId = talleres.FirstOrDefault()?.Id;
        r.TallerAsignadoId ??= r.TallerSugeridoId;
        r.MotivoSugerenciaTaller = HondurasLocationService.IsTegucigalpa(ciudad)
            ? "Tegucigalpa se gestiona por servicio al cliente"
            : talleres.Count == 0
            ? "No hay taller parametrizado"
            : talleres.First().Criterio;

        return GenerateMessage(r, talleres, empresa.TelefonoEmpresa, ciudad);
    }

    private static string GenerateMessage(ReclamoWhatsApp r, IEnumerable<TallerSugerido> talleresSugeridos, string? telefonoEmpresa = null, string? ciudad = null)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var fecha = r.FechaNotificacion?.ToString("dd/MM/yyyy") ?? "";
        var lugar = string.IsNullOrWhiteSpace(r.LugarAccidente) ? "el lugar indicado en el reclamo" : r.LugarAccidente;
        var talleres = talleresSugeridos.Take(5).ToList();
        var bloqueTalleres = HondurasLocationService.IsTegucigalpa(ciudad)
            ? @"
Para reclamos en Tegucigalpa, por favor coordine el seguimiento con nuestro servicio al cliente al numero 89659690. Con gusto le apoyaremos para indicarle el proceso con la aseguradora y agilizar la gestion."
            : talleres.Count == 0
            ? $@"
Para coordinacion de taller o inspeccion, puede contactarse con la aseguradora o con nosotros para indicarle el proceso correspondiente.{(string.IsNullOrWhiteSpace(telefonoEmpresa) ? "" : $"{Environment.NewLine}Telefono empresa: {telefonoEmpresa}")}"
            : $@"
Talleres en red:
{string.Join(Environment.NewLine, talleres.Select(x => $"- {x.Nombre}: {x.Direccion}{(string.IsNullOrWhiteSpace(x.Telefono) ? "" : $" / Tel. {x.Telefono}")}"))}";

        return $@"
Buenas tardes, {nombre}.

Reciba un cordial saludo. Le comunicamos que su reclamo fue notificado con fecha {fecha}, ocurrido en {lugar}.

Para poder avanzar con la gestion debe completar la siguiente documentacion:

1. Aviso de accidente original firmado por el conductor y asegurado. Si es empresa, aplicar sello correspondiente. Puede compartir este aviso al numero de servicio al cliente 89659690 para gestionar firma y sello, y agilizar el tramite con la aseguradora.
2. Certificacion de Transito.
3. Tarjeta de identidad del conductor, ambos lados.
4. Licencia del conductor, ambos lados.
5. Boleta de circulacion del vehiculo asegurado.
6. Inspeccion puntual de danos en Seguros Crefisa.
7. Dos cotizaciones de talleres de la red, cuando aplique.

{bloqueTalleres}

Una vez se entregue completa la informacion, se evaluara la cobertura y la aplicacion de deducibles.

Atentamente.".Trim();
    }

}
