namespace ReclamosWhatsApp.Models;

public class EnvioAutomaticoConfig
{
    public bool AutoEnviarReclamos { get; set; }
    public bool AutoEnviarRecordatoriosPago { get; set; }
    public bool AutoEnviarRecordatoriosPoliza { get; set; }
    public string DiasAntesVencimientoCuota { get; set; } = "7,3,1";
    public string DiasDespuesCuotaVencida { get; set; } = "1,3,7,15";
    public string DiasAntesVencimientoPoliza { get; set; } = "30,15,7";
    public int DiasEntreRecordatoriosReclamo { get; set; } = 1;
    public int MaxRecordatoriosReclamo { get; set; } = 3;
    public string PlantillaPagoProximo { get; set; } = "Estimado(a) {cliente}, le recordamos que la cuota de su poliza {poliza} vence el {fecha_vencimiento} por un monto de {monto}.";
    public string PlantillaPagoVencido { get; set; } = "Estimado(a) {cliente}, su cuota de la poliza {poliza} se encuentra vencida desde el {fecha_vencimiento} por un monto de {monto}.";
    public string PlantillaPolizaPorVencer { get; set; } = "Estimado(a) {cliente}, su poliza {poliza} de {aseguradora} vence el {fecha_vencimiento}. Podemos apoyarle con la renovacion.";
    public string PlantillaPolizaVencida { get; set; } = "Estimado(a) {cliente}, su poliza {poliza} vencio el {fecha_vencimiento}. Podemos apoyarle con su renovacion.";
    public string PlantillaReclamo { get; set; } = "Estimado(a) {cliente}, hemos recibido el reclamo {reclamo} de la poliza {poliza}. Le estaremos dando seguimiento.";
}
