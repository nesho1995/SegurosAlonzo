using ClosedXML.Excel;

namespace ReclamosWhatsApp.Services;

public class PlantillaCargaService
{
    public byte[] CrearPlantillaCartera()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Cartera");
        var headers = new[]
        {
            "NOMBRE", "COMPANIA SEGUROS", "RAMO", "CUOTAS", "FORMA PAGO", "POLIZA",
            "CERTIFICADO", "ENDOSO", "PRIMA NETA", "SEGURO ASIENTO", "PRIMA COMERCIAL",
            "IMPUESTO", "GASTOS EMISION", "BOMBEROS", "PRIMA TOTAL", "PLAN",
            "SUMA ASEGURADA", "VIGENCIA", "HASTA", "MEDIO", "VEHICULO", "CONTACTO",
            "CORREO", "AGENTE/OBSERVACIONES", "CUMPLEANOS", "OBSERVACION 2", "EMISION/RENOVACION"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F2EF");
        }

        ws.Cell(2, 1).Value = "Cliente Ejemplo";
        ws.Cell(2, 2).Value = "Aseguradora Ejemplo";
        ws.Cell(2, 3).Value = "Vehiculo";
        ws.Cell(2, 4).Value = 4;
        ws.Cell(2, 5).Value = "Mensual";
        ws.Cell(2, 6).Value = "POL-0001";
        ws.Cell(2, 15).Value = 12000;
        ws.Cell(2, 18).Value = "01/01/2026";
        ws.Cell(2, 19).Value = "31/12/2026";
        ws.Cell(2, 21).Value = "Toyota Corolla 2022";
        ws.Cell(2, 22).Value = "50499999999";
        ws.Cell(2, 23).Value = "cliente@correo.com";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] CrearPlantillaCarteraFinanciera()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Cartera financiera");
        var headers = new[]
        {
            "FINANCIERA", "COMPANIA SEGUROS", "POLIZA", "CLIENTE", "MARCA", "MODELO", "AÑO", "COLOR", "TIPO", "PLACA", "MOTOR",
            "VIN/SERIE", "CHASIS", "SUMA ASEGURADA", "VIGENCIA", "AGENTE/OBSERVACIONES", "CUOTAS",
            "CUOTA 1", "CUOTA 2", "CUOTA 3", "CUOTA 4", "CUOTA 5", "CUOTA 6", "CUOTA 7", "CUOTA 8",
            "CUOTA 9", "CUOTA 10", "CUOTA 11", "CUOTA 12", "PRIMA NETA", "PRIMA TOTAL", "RAMO"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F2EF");
        }

        ws.Cell(2, 1).Value = "BANCO ATLANTIDA";
        ws.Cell(2, 2).Value = "SEGUROS ALONZO";
        ws.Cell(2, 3).Value = "ZN AT 59545 2026";
        ws.Cell(2, 4).Value = "JUAN PEREZ";
        ws.Cell(2, 5).Value = "HYUNDAI";
        ws.Cell(2, 6).Value = "ELANTRA";
        ws.Cell(2, 7).Value = 2013;
        ws.Cell(2, 8).Value = "AZUL";
        ws.Cell(2, 9).Value = "TURISMO";
        ws.Cell(2, 10).Value = "HAG6713";
        ws.Cell(2, 11).Value = "G4NB-CU063155";
        ws.Cell(2, 12).Value = "5NPDH4AE3DH194382";
        ws.Cell(2, 14).Value = 140000;
        ws.Cell(2, 15).Value = "30/03/2026";
        ws.Cell(2, 16).Value = "AMAURY PORTILLO";
        ws.Cell(2, 17).Value = 8;
        ws.Cell(2, 18).Value = 22631.24;
        ws.Cell(2, 19).Value = 22631.24;
        ws.Cell(2, 20).Value = 22631.24;
        ws.Cell(2, 21).Value = 22631.24;
        ws.Cell(2, 22).Value = 22631.24;
        ws.Cell(2, 23).Value = 22631.24;
        ws.Cell(2, 24).Value = 22631.24;
        ws.Cell(2, 25).Value = 22631.26;
        ws.Cell(2, 30).Value = 158138.71;
        ws.Cell(2, 31).Value = 181049.94;
        ws.Cell(2, 32).Value = "AUTO";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
