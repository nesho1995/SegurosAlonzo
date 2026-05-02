using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Services;

public class CarteraImportService
{
    private static readonly string[] Fields =
    [
        "NOMBRE", "CLIENTE", "CLIENTE FINANCIERA", "CLIENTE CONTRATANTE", "COMPANIA SEGUROS", "RAMO", "CUOTAS", "FORMA PAGO", "POLIZA", "N", "CERTIFICADO", "ENDOSO",
        "PRIMA NETA", "SEGURO ASIENTO", "PRIMA COMERCIAL", "IMPUESTO", "GASTOS EMISION", "BOMBEROS",
        "PRIMA TOTAL", "PLAN", "SUMA ASEGURADA", "MESINICIODEPOLIZA", "VIGENCIA", "FECHA INGRESO", "HASTA", "MEDIO", "VEHICULO",
        "MARCA", "MODELO", "ANO", "COLOR", "TIPO", "PLACA", "MOTOR", "VIN/SERIE", "CHASIS", "AGENTE",
        "CUOTA 1", "CUOTA 2", "CUOTA 3", "CUOTA 4", "CUOTA 5", "CUOTA 6", "CUOTA 7", "CUOTA 8", "CUOTA 9", "CUOTA 10", "CUOTA 11", "CUOTA 12",
        "CONTACTO", "CORREO", "OBSERVACIONES", "OBSERVACION 2", "CUMPLEANOS", "EMISION/RENOVACION", "IDENTIDAD", "CIUDAD"
    ];

    private readonly CarteraRepository _repo;
    private readonly CatalogoRepository _catalogos;
    private readonly PhoneNormalizationService _phones;
    private readonly AuditoriaService _auditoria;
    private readonly NotificacionRepository _notificaciones;
    private readonly PolizaImportRulesService _historicalRules;

    public CarteraImportService(CarteraRepository repo, CatalogoRepository catalogos, PhoneNormalizationService phones, AuditoriaService auditoria, NotificacionRepository notificaciones, PolizaImportRulesService historicalRules)
    {
        _repo = repo;
        _catalogos = catalogos;
        _phones = phones;
        _auditoria = auditoria;
        _notificaciones = notificaciones;
        _historicalRules = historicalRules;
    }

    public async Task<(int clientes, int polizas)> ImportarAsync(Stream archivo)
    {
        var preview = Preview(archivo);
        var result = await ImportarPreviewDetalladoAsync(preview.Rows);
        return (result.Clientes, result.Polizas);
    }

    public CarteraImportPreview Preview(Stream archivo)
    {
        using var workbook = new XLWorkbook(archivo);
        var ws = workbook.Worksheets.First();
        var map = BuildHeaderMap(ws);
        var seenPolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = ws.RowsUsed()
            .Skip(1)
            .Select(row => BuildRowPreview(row, map, seenPolicies))
            .Where(row => !row.IsEmpty)
            .ToList();

        return new CarteraImportPreview
        {
            Rows = rows,
            TotalRows = rows.Count,
            ErrorCount = rows.Sum(x => x.Errors.Count),
            WarningCount = rows.Sum(x => x.Warnings.Count)
        };
    }

    public async Task<(int clientes, int polizas)> ImportarPreviewAsync(IEnumerable<CarteraImportRowPreview> rows)
    {
        var result = await ImportarPreviewDetalladoAsync(rows);
        return (result.Clientes, result.Polizas);
    }

    public async Task<CarteraImportResult> ImportarPreviewDetalladoAsync(IEnumerable<CarteraImportRowPreview> rows)
    {
        await _repo.EnsureImportSchemaAsync();
        var validRows = rows.Where(x => x.Errors.Count == 0).ToList();
        var rejectedRows = rows.Where(x => x.Errors.Count > 0).ToList();
        var clientesProcesados = new HashSet<int>();
        var polizasProcesadas = 0;
        var polizasDuplicadas = 0;

        foreach (var row in validRows)
        {
            var nombre = row.GetClean("NOMBRE");
            var clienteId = await _repo.GetOrCreateClienteAsync(nombre, row.PhoneNormalization.Principal, row.EmailNormalization.Principal);
            var contratanteNombre = EmptyToNull(row.GetClean("CLIENTE FINANCIERA")) ?? EmptyToNull(row.GetClean("CLIENTE CONTRATANTE"));
            var contratanteId = !string.IsNullOrWhiteSpace(contratanteNombre)
                ? await _repo.GetOrCreateClienteAsync(contratanteNombre)
                : (int?)null;
            var observaciones = BuildClientObservaciones(row);
            var sumaMedica = ParseSumaAseguradaMedica(row.GetClean("RAMO"), row.GetOriginal("SUMA ASEGURADA"), row.GetClean("SUMA ASEGURADA"));
            var ramo = ParseRamo(row.GetClean("RAMO"));
            await _catalogos.EnsureValueAsync("RAMOS", row.GetClean("RAMO"), ramo.RequiresReview);
            await _catalogos.EnsureValueAsync("COMPANIAS", row.GetClean("COMPANIA SEGUROS"), false);
            await _catalogos.EnsureValueAsync("FORMAS_PAGO", row.GetClean("FORMA PAGO"), false);
            await _catalogos.EnsureValueAsync("MEDIOS", row.GetClean("MEDIO"), false);

            var cliente = new Cliente
            {
                Nombre = nombre,
                Telefono = EmptyToNull(row.PhoneNormalization.Principal),
                TelefonoSecundario = EmptyToNull(row.PhoneNormalization.Secondary),
                TelefonosExtraJson = row.PhoneNormalization.Extras.Count > 0 ? JsonSerializer.Serialize(row.PhoneNormalization.Extras) : null,
                Contacto = EmptyToNull(row.PhoneNormalization.Secondary),
                Email = EmptyToNull(row.EmailNormalization.Principal),
                CorreosExtraJson = row.EmailNormalization.Extras.Count > 0 ? JsonSerializer.Serialize(row.EmailNormalization.Extras) : null,
                Identidad = EmptyToNull(row.GetClean("IDENTIDAD")),
                Ciudad = EmptyToNull(row.GetClean("CIUDAD")),
                FechaNacimiento = ParseDate(row.GetClean("CUMPLEANOS")),
                Observaciones = EmptyToNull(observaciones),
                RequiereRevisionManual = !row.PhoneNormalization.WhatsappReady || ramo.RequiresReview || !sumaMedica.Parseada,
                EstadoRevision = !row.PhoneNormalization.WhatsappReady || ramo.RequiresReview || !sumaMedica.Parseada ? "PENDIENTE_REVISION" : "OK",
                MotivoRevision = BuildClienteRevisionReason(row, ramo, sumaMedica),
                NotasCalidadJson = BuildQualityNotes(row, includeContact: true),
                Activo = true
            };

            await _repo.ActualizarDatosClienteAsync(clienteId, cliente);
            foreach (var phone in row.PhoneNormalization.Valid)
                await _repo.InsertTelefonoAsync(clienteId, phone.E164, phone == row.PhoneNormalization.PrincipalPhone);

            clientesProcesados.Add(clienteId);
            var numeroPoliza = EmptyToNull(row.GetClean("POLIZA"));
            if (string.IsNullOrWhiteSpace(numeroPoliza))
            {
                await AuditRowAsync(row, clienteId, cliente, null, null, null);
                continue;
            }

            var vehiculo = BuildVehiculo(row, clienteId);
            var vehiculoId = await _repo.UpsertVehiculoAsync(vehiculo);
            var fechaInicio = ParseDate(row.GetClean("VIGENCIA")) ?? ParseDate(row.GetClean("FECHA INGRESO"));
            var cuotas = ResolveCuotas(row);
            var montosCuotas = BuildCuotasMontos(row, cuotas);

            var poliza = new Poliza
            {
                ClienteId = clienteId,
                ClienteContratanteId = contratanteId,
                VehiculoId = vehiculoId,
                Aseguradora = EmptyToNull(row.GetClean("COMPANIA SEGUROS")),
                Ramo = EmptyToNull(row.GetClean("RAMO")),
                RamoNormalizado = ramo.Normalized,
                ExtrasJson = ramo.ExtrasJson,
                Cuotas = cuotas > 0 ? cuotas : null,
                FormaPago = EmptyToNull(row.GetClean("FORMA PAGO")),
                NumeroPoliza = numeroPoliza,
                NumeroItem = EmptyToNull(row.GetClean("N")),
                Certificado = EmptyToNull(row.GetClean("CERTIFICADO")),
                Endoso = EmptyToNull(row.GetClean("ENDOSO")),
                PrimaNeta = ParseMoney(row.GetClean("PRIMA NETA")).Value,
                SeguroAsiento = ParseMoney(row.GetClean("SEGURO ASIENTO")).Value,
                PrimaComercial = ParseMoney(row.GetClean("PRIMA COMERCIAL")).Value,
                Impuesto = ParseMoney(row.GetClean("IMPUESTO")).Value,
                GastosEmision = ParseMoney(row.GetClean("GASTOS EMISION")).Value,
                Bomberos = ParseMoney(row.GetClean("BOMBEROS")).Value,
                PrimaTotal = ParseMoney(row.GetClean("PRIMA TOTAL")).Value,
                Plan = EmptyToNull(row.GetClean("PLAN")),
                SumaAsegurada = sumaMedica.SumaAseguradaVida ?? ParseMoney(row.GetClean("SUMA ASEGURADA")).Value,
                SumaAseguradaTextoOriginal = EmptyToNull(sumaMedica.TextoOriginal),
                MaximoVitalicio = sumaMedica.MaximoVitalicio,
                SumaAseguradaVida = sumaMedica.SumaAseguradaVida,
                MesInicioPoliza = EmptyToNull(row.GetClean("MESINICIODEPOLIZA")),
                Vigencia = fechaInicio,
                FechaInicio = fechaInicio,
                Hasta = ParseDate(row.GetClean("HASTA")),
                Medio = EmptyToNull(row.GetClean("MEDIO")),
                Vehiculo = EmptyToNull(BuildVehiculoResumen(row)) ?? EmptyToNull(row.GetClean("VEHICULO")),
                Marca = EmptyToNull(row.GetClean("MARCA")),
                Modelo = EmptyToNull(row.GetClean("MODELO")),
                Anio = ParseInt(row.GetClean("ANO")),
                Color = EmptyToNull(row.GetClean("COLOR")),
                TipoVehiculo = EmptyToNull(row.GetClean("TIPO")),
                Placa = EmptyToNull(row.GetClean("PLACA")),
                Motor = EmptyToNull(row.GetClean("MOTOR")),
                VinSerie = EmptyToNull(row.GetClean("VIN/SERIE")),
                Chasis = EmptyToNull(row.GetClean("CHASIS")),
                AgenteAsignado = EmptyToNull(row.GetClean("AGENTE")),
                EmisionRenovacion = EmptyToNull(row.GetClean("EMISION/RENOVACION")),
                Observacion2 = EmptyToNull(row.GetClean("OBSERVACION 2")),
                Observaciones = EmptyToNull(BuildPolicyObservaciones(row)),
                ObservacionOriginal = EmptyToNull($"{row.GetClean("OBSERVACIONES")} {row.GetClean("OBSERVACION 2")}".Trim()),
                RequiereRevisionManual = ramo.RequiresReview || !sumaMedica.Parseada || !row.PhoneNormalization.WhatsappReady,
                EstadoRevision = ramo.RequiresReview || !sumaMedica.Parseada || !row.PhoneNormalization.WhatsappReady ? "PENDIENTE_REVISION" : "OK",
                MotivoRevision = BuildPolizaRevisionReason(ramo, sumaMedica, row),
                NotasCalidadJson = BuildQualityNotes(row, includeContact: false),
                EstadoPago = "SIN_VALIDAR",
                Activo = true
            };

            _historicalRules.ApplyHistoricalRules(poliza, new HistoricalRulesContext
            {
                RamoRaw = row.GetClean("RAMO"),
                FormaPagoRaw = row.GetClean("FORMA PAGO"),
                EmisionRenovacionRaw = row.GetClean("EMISION/RENOVACION"),
                Observacion2 = row.GetClean("OBSERVACION 2"),
                Observaciones = row.GetClean("OBSERVACIONES"),
                SumaAseguradaOriginal = row.GetOriginal("SUMA ASEGURADA"),
                SumaAseguradaLimpia = row.GetClean("SUMA ASEGURADA"),
                Cliente = cliente,
                AllowOverwriteAutomaticOnly = false
            });
            poliza.Observaciones = EmptyToNull(BuildPolicyObservaciones(row)) ?? poliza.Observaciones;
            poliza.AgenteAsignado = EmptyToNull(row.GetClean("AGENTE"));

            await _catalogos.EnsureValueAsync("ESTADOS_PAGO", poliza.EstadoPago, false);
            if (!string.IsNullOrWhiteSpace(poliza.TipoProceso))
                await _catalogos.EnsureValueAsync("TIPO_PROCESO", poliza.TipoProceso, false);
            if (!string.IsNullOrWhiteSpace(poliza.EstadoPolizaReal))
                await _catalogos.EnsureValueAsync("ESTADOS_POLIZA", poliza.EstadoPolizaReal, false);
            if (!string.IsNullOrWhiteSpace(poliza.EmisionRenovacion))
                await _catalogos.EnsureValueAsync("EMISION_RENOVACION", poliza.EmisionRenovacion, false);
            await _catalogos.EnsureValueAsync("ESTADO_REVISION", poliza.EstadoRevision, false);

            var (polizaId, inserted) = await _repo.InsertPolizaIfNotExistsDetailedAsync(poliza);
            if (inserted)
                polizasProcesadas++;
            else
                polizasDuplicadas++;

            if (cuotas > 0 && fechaInicio.HasValue)
                await _repo.UpsertCuotasAsync(polizaId, cuotas, fechaInicio.Value.Date, montosCuotas);

            await AuditRowAsync(row, clienteId, cliente, poliza, sumaMedica, ramo);
        }

        return new CarteraImportResult
        {
            Clientes = clientesProcesados.Count,
            Polizas = polizasProcesadas,
            PolizasDuplicadas = polizasDuplicadas,
            FilasImportadas = validRows.Count,
            FilasRechazadas = rejectedRows.Count
        };
    }

    public byte[] CrearReporteErrores(CarteraImportPreview preview)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("rechazadas");
        var headers = new[] { "Fila", "Estado", "Campo", "Original", "Limpio", "Severidad", "Mensaje", "TelefonoWhatsApp", "TelefonoCloud" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var excelRow = 2;
        foreach (var row in preview.Rows.Where(x => x.Errors.Count > 0))
        {
            foreach (var issue in row.Errors.Select(x => ("ERROR", x)).Concat(row.Warnings.Select(x => ("ADVERTENCIA", x))))
            {
                var field = row.Values.FirstOrDefault(x => SameField(x.Field, issue.x.Field));
                ws.Cell(excelRow, 1).Value = row.RowNumber;
                ws.Cell(excelRow, 2).Value = row.Estado;
                ws.Cell(excelRow, 3).Value = issue.x.Field;
                ws.Cell(excelRow, 4).Value = field?.Original ?? "";
                ws.Cell(excelRow, 5).Value = field?.Clean ?? "";
                ws.Cell(excelRow, 6).Value = issue.Item1;
                ws.Cell(excelRow, 7).Value = issue.x.Message;
                ws.Cell(excelRow, 8).Value = row.PhoneNormalization.Principal;
                ws.Cell(excelRow, 9).Value = row.PhoneNormalization.PrincipalWhatsApp;
                excelRow++;
            }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] CrearExcelLimpio(CarteraImportPreview preview)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("cartera_limpia");
        var headers = new[]
        {
            "Fila", "Estado", "NombreOriginal", "NombreLimpio", "ContactoOriginal", "TelefonoWhatsAppE164",
            "TelefonoWhatsAppCloudApi", "WhatsappReady", "TelefonoSecundario", "TelefonosExtraJson", "TelefonosInvalidos",
            "CorreoOriginal", "CorreoPrincipal", "CorreosExtraJson", "Aseguradora", "Ramo", "Poliza", "Certificado",
            "Endoso", "Cuotas", "FormaPago", "PrimaNeta", "SeguroAsiento", "PrimaComercial", "Impuesto", "GastosEmision",
            "Bomberos", "PrimaTotal", "SumaAsegurada", "MesInicioPoliza", "Vigencia", "Hasta", "Cumpleanos", "Medio", "Vehiculo",
            "EmisionRenovacion", "Observaciones", "Errores", "Advertencias"
        };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var excelRow = 2;
        foreach (var row in preview.Rows)
        {
            ws.Cell(excelRow, 1).Value = row.RowNumber;
            ws.Cell(excelRow, 2).Value = row.Estado;
            ws.Cell(excelRow, 3).Value = row.GetOriginal("NOMBRE");
            ws.Cell(excelRow, 4).Value = row.GetClean("NOMBRE");
            ws.Cell(excelRow, 5).Value = row.GetOriginal("CONTACTO");
            ws.Cell(excelRow, 6).Value = row.PhoneNormalization.Principal;
            ws.Cell(excelRow, 7).Value = row.PhoneNormalization.PrincipalWhatsApp;
            ws.Cell(excelRow, 8).Value = row.PhoneNormalization.WhatsappReady;
            ws.Cell(excelRow, 9).Value = row.PhoneNormalization.Secondary;
            ws.Cell(excelRow, 10).Value = JsonSerializer.Serialize(row.PhoneNormalization.Extras);
            ws.Cell(excelRow, 11).Value = string.Join(", ", row.PhoneNormalization.Invalid);
            ws.Cell(excelRow, 12).Value = row.GetOriginal("CORREO");
            ws.Cell(excelRow, 13).Value = row.EmailNormalization.Principal;
            ws.Cell(excelRow, 14).Value = JsonSerializer.Serialize(row.EmailNormalization.Extras);
            ws.Cell(excelRow, 15).Value = row.GetClean("COMPANIA SEGUROS");
            ws.Cell(excelRow, 16).Value = row.GetClean("RAMO");
            ws.Cell(excelRow, 17).Value = row.GetClean("POLIZA");
            ws.Cell(excelRow, 18).Value = row.GetClean("CERTIFICADO");
            ws.Cell(excelRow, 19).Value = row.GetClean("ENDOSO");
            ws.Cell(excelRow, 20).Value = ParseInt(row.GetClean("CUOTAS"));
            ws.Cell(excelRow, 21).Value = row.GetClean("FORMA PAGO");
            ws.Cell(excelRow, 22).Value = ParseMoney(row.GetClean("PRIMA NETA")).Value;
            ws.Cell(excelRow, 23).Value = ParseMoney(row.GetClean("SEGURO ASIENTO")).Value;
            ws.Cell(excelRow, 24).Value = ParseMoney(row.GetClean("PRIMA COMERCIAL")).Value;
            ws.Cell(excelRow, 25).Value = ParseMoney(row.GetClean("IMPUESTO")).Value;
            ws.Cell(excelRow, 26).Value = ParseMoney(row.GetClean("GASTOS EMISION")).Value;
            ws.Cell(excelRow, 27).Value = ParseMoney(row.GetClean("BOMBEROS")).Value;
            ws.Cell(excelRow, 28).Value = ParseMoney(row.GetClean("PRIMA TOTAL")).Value;
            ws.Cell(excelRow, 29).Value = ParseMoney(row.GetClean("SUMA ASEGURADA")).Value;
            ws.Cell(excelRow, 30).Value = row.GetClean("MESINICIODEPOLIZA");
            ws.Cell(excelRow, 31).Value = FormatDate(row.GetClean("VIGENCIA"));
            ws.Cell(excelRow, 32).Value = FormatDate(row.GetClean("HASTA"));
            ws.Cell(excelRow, 33).Value = FormatDate(row.GetClean("CUMPLEANOS"));
            ws.Cell(excelRow, 34).Value = row.GetClean("MEDIO");
            ws.Cell(excelRow, 35).Value = row.GetClean("VEHICULO");
            ws.Cell(excelRow, 36).Value = row.GetClean("EMISION/RENOVACION");
            ws.Cell(excelRow, 37).Value = BuildClientObservaciones(row);
            ws.Cell(excelRow, 38).Value = string.Join(" | ", row.Errors.Select(x => $"{x.Field}: {x.Message}"));
            ws.Cell(excelRow, 39).Value = string.Join(" | ", row.Warnings.Select(x => $"{x.Field}: {x.Message}"));
            excelRow++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private CarteraImportRowPreview BuildRowPreview(IXLRow row, Dictionary<string, int> map, HashSet<string> seenPolicies)
    {
        var preview = new CarteraImportRowPreview { RowNumber = row.RowNumber() };
        foreach (var field in Fields)
        {
            var original = Get(row, map, field);
            preview.Values.Add(new CarteraImportFieldValue(field, original, CleanText(original)));
        }

        if (string.IsNullOrWhiteSpace(preview.GetClean("NOMBRE")) && !string.IsNullOrWhiteSpace(preview.GetClean("CLIENTE")))
            preview.SetClean("NOMBRE", preview.GetClean("CLIENTE"));
        if (IsPlaceholderClientName(preview.GetClean("NOMBRE")))
        {
            var fallbackNombre = new[]
            {
                preview.GetClean("CLIENTE"),
                preview.GetClean("NOMBREASEGURADO"),
                preview.GetClean("ASEGURADO")
            }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !IsPlaceholderClientName(value));

            if (!string.IsNullOrWhiteSpace(fallbackNombre))
                preview.SetClean("NOMBRE", fallbackNombre);
        }
        if (string.IsNullOrWhiteSpace(preview.GetClean("VIGENCIA")) && !string.IsNullOrWhiteSpace(preview.GetClean("FECHA INGRESO")))
            preview.SetClean("VIGENCIA", preview.GetClean("FECHA INGRESO"));

        preview.PhoneNormalization = _phones.NormalizeMany(preview.GetOriginal("CONTACTO"));
        preview.SetClean("CONTACTO", preview.PhoneNormalization.Principal);
        preview.EmailNormalization = NormalizeEmails(preview.GetOriginal("CORREO"));
        preview.SetClean("CORREO", preview.EmailNormalization.Principal);

        CleanAndValidateRequiredText(preview, "NOMBRE", "El nombre del cliente es obligatorio.");
        ValidatePhones(preview);
        ValidateEmails(preview);
        ValidateDate(preview, "CUMPLEANOS", required: false);
        ValidateDate(preview, "VIGENCIA", required: true);
        ValidateDate(preview, "FECHA INGRESO", required: false);
        ValidateDate(preview, "HASTA", required: false);
        ValidateCuotas(preview);

        foreach (var field in new[] { "PRIMA NETA", "SEGURO ASIENTO", "PRIMA COMERCIAL", "IMPUESTO", "GASTOS EMISION", "BOMBEROS", "PRIMA TOTAL", "SUMA ASEGURADA", "CUOTA 1", "CUOTA 2", "CUOTA 3", "CUOTA 4", "CUOTA 5", "CUOTA 6", "CUOTA 7", "CUOTA 8", "CUOTA 9", "CUOTA 10", "CUOTA 11", "CUOTA 12" })
            ValidateMoney(preview, field);

        ValidatePolicyDuplicates(preview, seenPolicies);
        if (string.IsNullOrWhiteSpace(preview.GetClean("POLIZA")))
            preview.Warnings.Add(new CarteraImportIssue("POLIZA", "Fila sin numero de poliza: se creara/actualizara solo el cliente."));
        return preview;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(header))
                map[header] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static string Get(IXLRow row, Dictionary<string, int> map, string key)
    {
        key = NormalizeHeader(key);
        if (!map.TryGetValue(key, out var col))
        {
            var aliases = key switch
            {
                "NOMBRE" => new[] { "CLIENTE", "CLIENTE2", "CLIENTE_2", "ASEGURADO", "NOMBREASEGURADO" },
                "CLIENTE" => new[] { "ASEGURADO", "NOMBREASEGURADO", "NOMBRECLIENTE" },
                "CLIENTEFINANCIERA" => new[] { "FINANCIERA", "CONTRATANTE", "CLIENTECONTRATANTE" },
                "CLIENTECONTRATANTE" => new[] { "CLIENTEFINANCIERA", "FINANCIERA", "CONTRATANTE" },
                "COMPANIASEGUROS" => new[] { "COMPANIA", "ASEGURADORA", "COMPANIASEGURO" },
                "POLIZA" => new[] { "NUMEROPOLIZA", "NOPOLIZA", "POLIZANUMERO", "NUMPOLIZA", "PÓLIZA" },
                "N" => new[] { "NUMEROITEM", "ITEM", "NO" },
                "CUOTAS" => new[] { "CUOTA", "NUMEROCUOTAS", "NOCUOTAS" },
                "VIGENCIA" => new[] { "FECHAINGRESO", "FECHADEINGRESO", "FECHAINICIO" },
                "MESINICIODEPOLIZA" => new[] { "MESINICIO", "MESINICIOPOLIZA", "MES INICIO" },
                "FECHAINGRESO" => new[] { "FECHADEINGRESO", "VIGENCIA", "FECHAINICIO" },
                "ANO" => new[] { "ANIO", "AÑO" },
                "VINSERIE" => new[] { "VIN", "SERIE", "VINSERIE" },
                "OBSERVACION2" => new[] { "OBSERVACIONES2" },
                "AGENTE" => new[] { "AGENTEOBSERVACIONES", "AGENTE/OBSERVACIONES" },
                "OBSERVACIONES" => new[] { "AGENTEOBSERVACIONES", "AGENTE/OBSERVACIONES" },
                "EMISIONRENOVACION" => new[] { "EMISIONORENOVACION", "EMISIONRENOVACION" },
                _ => Array.Empty<string>()
            };
            col = aliases.Select(alias => map.TryGetValue(alias, out var aliasCol) ? aliasCol : 0).FirstOrDefault(c => c > 0);
        }
        if (col <= 0)
            return "";

        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (cell.DataType == XLDataType.Number)
        {
            if (IsDateField(key))
            {
                var serial = cell.GetDouble();
                if (serial > 1)
                    return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
            {
                // Usar valor numérico crudo para evitar que formatos moneda tipo "L"#,##0.00
                // se rendericen mal en ClosedXML y rompan ParseMoney.
                return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
            }
        }

        return cell.GetFormattedString().Trim();
    }

    private static bool IsDateField(string field) => field is "VIGENCIA" or "FECHAINGRESO" or "FECHADEINGRESO" or "HASTA" or "CUMPLEANOS";

    private static void CleanAndValidateRequiredText(CarteraImportRowPreview preview, string field, string message)
    {
        preview.SetClean(field, CleanText(preview.GetClean(field)));
        if (string.IsNullOrWhiteSpace(preview.GetClean(field)))
            preview.Errors.Add(new CarteraImportIssue(field, message));
    }

    private static void ValidatePhones(CarteraImportRowPreview preview)
    {
        if (string.IsNullOrWhiteSpace(preview.GetOriginal("CONTACTO")))
            return;

        if (string.IsNullOrWhiteSpace(preview.PhoneNormalization.Principal))
            preview.Warnings.Add(new CarteraImportIssue("CONTACTO", "No se encontro telefono compatible con WhatsApp; no se guardara basura."));

        foreach (var invalid in preview.PhoneNormalization.Invalid)
            preview.Warnings.Add(new CarteraImportIssue("CONTACTO", $"No se pudo validar como telefono: {invalid}."));

        foreach (var note in preview.PhoneNormalization.Notes)
            preview.Warnings.Add(new CarteraImportIssue("CONTACTO", $"Texto conservado como nota, no como telefono: {note}."));
    }

    private static EmailNormalization NormalizeEmails(string raw)
    {
        var result = new EmailNormalization { Original = raw ?? "" };
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var originalRaw = raw.Trim();
        raw = Regex.Replace(originalRaw, @"@((?:gmail|yahoo|hotmail)),com\b", "@$1.com", RegexOptions.IgnoreCase);
        if (!string.Equals(originalRaw, raw, StringComparison.Ordinal))
            result.Suggestions.Add($"{originalRaw} -> {raw}");
        var candidates = Regex.Split(raw, @"[,;/\s]+")
            .Select(x => x.Trim().Trim('.').ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        foreach (var candidate in candidates)
        {
            var fixedCandidate = Regex.Replace(candidate, @"@(gmail|yahoo|hotmail),com$", "@$1.com", RegexOptions.IgnoreCase);
            if (IsEmail(fixedCandidate))
                result.Valid.Add(fixedCandidate);
            else
                result.Invalid.Add(candidate);

            if (!string.Equals(candidate, fixedCandidate, StringComparison.OrdinalIgnoreCase))
                result.Suggestions.Add($"{candidate} -> {fixedCandidate}");
        }

        result.Valid = result.Valid.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return result;
    }

    private static void ValidateEmails(CarteraImportRowPreview preview)
    {
        foreach (var invalid in preview.EmailNormalization.Invalid)
            preview.Warnings.Add(new CarteraImportIssue("CORREO", $"Correo no validable: {invalid}."));

        foreach (var suggestion in preview.EmailNormalization.Suggestions)
            preview.Warnings.Add(new CarteraImportIssue("CORREO", $"Se sugirio correccion: {suggestion}."));
    }

    private static bool IsEmail(string email) => Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    private static void ValidateDate(CarteraImportRowPreview preview, string field, bool required)
    {
        var value = preview.GetClean(field);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
                preview.Errors.Add(new CarteraImportIssue(field, "La fecha es obligatoria para crear la poliza."));
            return;
        }

        var date = ParseDate(value);
        if (date is null)
        {
            var issue = new CarteraImportIssue(field, "La fecha no tiene un formato reconocido.");
            if (required)
                preview.Errors.Add(issue);
            else
                preview.Warnings.Add(issue);
            return;
        }

        preview.SetClean(field, date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static void ValidateMoney(CarteraImportRowPreview preview, string field)
    {
        var value = preview.GetClean(field);
        if (string.IsNullOrWhiteSpace(value))
            return;

        var money = ParseMoney(value);
        if (money.MultipleValues)
        {
            if (SameField(field, "SUMA ASEGURADA") && SameField(preview.GetClean("RAMO"), "MEDICO"))
                preview.Warnings.Add(new CarteraImportIssue(field, "Suma asegurada medica compuesta detectada; se separara en maximo vitalicio y suma asegurada vida."));
            else
                preview.Warnings.Add(new CarteraImportIssue(field, "El campo trae mas de un monto; se conserva nota y no se fuerza un cero falso."));
        }
        else if (!money.Parsed)
            preview.Warnings.Add(new CarteraImportIssue(field, "No se pudo interpretar como monto; se guardara vacio."));

        preview.SetClean(field, money.Value.HasValue ? money.Value.Value.ToString(CultureInfo.InvariantCulture) : "");
    }

    private static void ValidateCuotas(CarteraImportRowPreview preview)
    {
        var value = preview.GetClean("CUOTAS");
        if (string.IsNullOrWhiteSpace(value))
        {
            var cuotasConMonto = CountCuotasConMonto(preview);
            if (cuotasConMonto > 0)
                preview.SetClean("CUOTAS", cuotasConMonto.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var cuotas = ParseInt(value);
        if (cuotas is null or < 0)
            preview.Warnings.Add(new CarteraImportIssue("CUOTAS", "La cantidad de cuotas no es valida; se guardara vacia."));
        else if (cuotas > 12)
        {
            preview.Warnings.Add(new CarteraImportIssue("CUOTAS", "La cantidad maxima soportada es 12; se importaran las primeras 12 cuotas."));
            preview.SetClean("CUOTAS", "12");
        }
        else
            preview.SetClean("CUOTAS", cuotas.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static void ValidatePolicyDuplicates(CarteraImportRowPreview preview, HashSet<string> seenPolicies)
    {
        var key = string.Join("|",
            NormalizeKey(preview.GetClean("NOMBRE")),
            NormalizeKey(preview.GetClean("COMPANIA SEGUROS")),
            NormalizeKey(preview.GetClean("POLIZA")),
            NormalizeKey(preview.GetClean("N")),
            NormalizeKey(preview.GetClean("CERTIFICADO")),
            NormalizeKey(preview.GetClean("ENDOSO")));

        if (!string.IsNullOrWhiteSpace(preview.GetClean("POLIZA")) && !seenPolicies.Add(key))
            preview.Warnings.Add(new CarteraImportIssue("POLIZA", "Fila repetida para el mismo cliente/poliza/item/certificado/endoso; se omitira para evitar duplicados exactos."));
    }

    private static MoneyParseResult ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new MoneyParseResult(null, true, false);

        var clean = value.Trim();
        if (Regex.IsMatch(clean, @"^[Ll$]?\s*-+$"))
            return new MoneyParseResult(null, true, false);

        if (Regex.IsMatch(clean, @"\d\s*/\s*\d"))
            return new MoneyParseResult(null, false, true);

        clean = clean.Replace("L", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return new MoneyParseResult(result, true, false);

        return new MoneyParseResult(null, false, false);
    }

    private static int? ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = Regex.Replace(value, @"[^\d-]", "");
        return int.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = NormalizeSpanishMonth(value.Trim().ToLowerInvariant());
        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd-MMM-yy", "d-MMM-yy",
            "dd-MMM-yyyy", "d-MMM-yyyy", "dd/MM/yy", "d/M/yy"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return NormalizeTwoDigitYear(fecha);

        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial) && serial > 1)
            return DateTime.FromOADate(serial);

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
            ? NormalizeTwoDigitYear(fecha)
            : null;
    }

    private static DateTime NormalizeTwoDigitYear(DateTime date)
    {
        if (date.Year > DateTime.Today.Year + 20)
            return date.AddYears(-100);
        return date;
    }

    private static string NormalizeSpanishMonth(string value)
    {
        return value
            .Replace("ene", "jan").Replace("abr", "apr").Replace("ago", "aug").Replace("dic", "dec")
            .Replace("sept", "sep");
    }

    private static string FormatDate(string value)
    {
        var date = ParseDate(value);
        return date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
    }

    private static Vehiculo BuildVehiculo(CarteraImportRowPreview row, int clienteId)
    {
        return new Vehiculo
        {
            ClienteId = clienteId,
            Marca = EmptyToNull(row.GetClean("MARCA")),
            Modelo = EmptyToNull(row.GetClean("MODELO")),
            Anio = ParseInt(row.GetClean("ANO")),
            Color = EmptyToNull(row.GetClean("COLOR")),
            Tipo = EmptyToNull(row.GetClean("TIPO")),
            Placa = EmptyToNull(row.GetClean("PLACA")),
            Motor = EmptyToNull(row.GetClean("MOTOR")),
            VinSerie = EmptyToNull(row.GetClean("VIN/SERIE")),
            Chasis = EmptyToNull(row.GetClean("CHASIS")),
            OrigenDatos = "IMPORTACION_EXCEL"
        };
    }

    private static string BuildVehiculoResumen(CarteraImportRowPreview row)
    {
        return string.Join(" ", new[]
        {
            row.GetClean("MARCA"),
            row.GetClean("MODELO"),
            row.GetClean("ANO"),
            row.GetClean("PLACA")
        }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private static IReadOnlyList<decimal?> BuildCuotasMontos(CarteraImportRowPreview row, int cuotas)
    {
        var result = Enumerable.Repeat<decimal?>(null, 12).ToArray();
        cuotas = Math.Clamp(cuotas, 0, 12);
        if (cuotas == 0)
            return result;

        var explicitValues = Enumerable.Range(1, cuotas)
            .Select(i => ParseMoney(row.GetClean($"CUOTA {i}")).Value)
            .ToArray();

        if (explicitValues.Any(x => x.HasValue))
        {
            for (var i = 0; i < cuotas; i++)
                result[i] = explicitValues[i] ?? 0m;
            return result;
        }

        // Regla operativa: si no vienen montos de cuotas en el archivo,
        // se cargan en 0 para captura manual posterior contra recibo/poliza fisica.
        for (var i = 0; i < cuotas; i++)
            result[i] = 0m;
        return result;
    }

    private static int ResolveCuotas(CarteraImportRowPreview row)
    {
        var cuotasDeclaradas = ParseInt(row.GetClean("CUOTAS")) ?? 0;
        if (cuotasDeclaradas > 0)
            return Math.Min(cuotasDeclaradas, 12);

        var cuotasConMonto = CountCuotasConMonto(row);
        if (cuotasConMonto > 0)
            return cuotasConMonto;

        var formaPago = NormalizeHeader(row.GetClean("FORMA PAGO"));
        if (formaPago is "CONTADO" or "EXTRA")
            return 1;

        return 0;
    }

    private static int CountCuotasConMonto(CarteraImportRowPreview row)
    {
        var max = 0;
        for (var i = 1; i <= 12; i++)
        {
            var money = ParseMoney(row.GetClean($"CUOTA {i}"));
            if (money.Parsed && money.Value.HasValue)
                max = i;
        }

        return max;
    }

    private static string BuildClientObservaciones(CarteraImportRowPreview row)
    {
        var parts = new List<string>();
        var clasificada = ClassifyObservaciones(row.GetClean("OBSERVACIONES"), row.GetClean("OBSERVACION 2"));
        if (clasificada.Tipo is "GENERAL" or "REFERIDO")
            parts.Add(clasificada.Original);

        var dataQuality = new
        {
            telefonos_extra = row.PhoneNormalization.Extras,
            telefonos_invalidos = row.PhoneNormalization.Invalid,
            notas_contacto = row.PhoneNormalization.Notes,
            correos_extra = row.EmailNormalization.Extras,
            correos_invalidos = row.EmailNormalization.Invalid,
            correcciones_correo = row.EmailNormalization.Suggestions,
            advertencias = row.Warnings.Select(x => $"{x.Field}: {x.Message}").ToArray()
        };

        if (clasificada.Tipo == "BASURA_TECNICA"
            || dataQuality.telefonos_extra.Count > 0
            || dataQuality.telefonos_invalidos.Count > 0
            || dataQuality.notas_contacto.Count > 0
            || dataQuality.correos_extra.Count > 0
            || dataQuality.correos_invalidos.Count > 0
            || dataQuality.correcciones_correo.Count > 0
            || dataQuality.advertencias.Length > 0)
        {
            parts.Add("Notas de limpieza: " + JsonSerializer.Serialize(dataQuality));
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string BuildPolicyObservaciones(CarteraImportRowPreview row)
    {
        var policyWarnings = row.Warnings
            .Where(x => !SameField(x.Field, "CONTACTO") && !SameField(x.Field, "CORREO") && !SameField(x.Field, "CUMPLEANOS"))
            .Select(x => $"{x.Field}: {x.Message}")
            .ToArray();

        if (policyWarnings.Length == 0)
        {
            var basicParts = new[]
            {
                string.IsNullOrWhiteSpace(row.GetClean("AGENTE")) ? "" : $"Agente: {row.GetClean("AGENTE")}",
                string.IsNullOrWhiteSpace(row.GetClean("CLIENTE FINANCIERA")) ? "" : $"Cliente empresa/financiera: {row.GetClean("CLIENTE FINANCIERA")}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return basicParts.Length == 0 ? "" : string.Join(Environment.NewLine, basicParts);
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Notas de limpieza de poliza: " + JsonSerializer.Serialize(policyWarnings),
            string.IsNullOrWhiteSpace(row.GetClean("AGENTE")) ? "" : $"Agente: {row.GetClean("AGENTE")}",
            string.IsNullOrWhiteSpace(row.GetClean("CLIENTE FINANCIERA")) ? "" : $"Cliente empresa/financiera: {row.GetClean("CLIENTE FINANCIERA")}"
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? BuildQualityNotes(CarteraImportRowPreview row, bool includeContact)
    {
        var warnings = row.Warnings
            .Where(x => includeContact || (!SameField(x.Field, "CONTACTO") && !SameField(x.Field, "CORREO") && !SameField(x.Field, "CUMPLEANOS")))
            .Select(x => new { campo = x.Field, nota = x.Message })
            .ToArray();

        return warnings.Length == 0 ? null : JsonSerializer.Serialize(warnings);
    }

    private async Task AuditRowAsync(CarteraImportRowPreview row, int clienteId, Cliente cliente, Poliza? poliza, SumaAseguradaMedica? suma, RamoParseResult? ramo)
    {
        if (!row.PhoneNormalization.WhatsappReady)
        {
            await _auditoria.RegistrarAsync("TELEFONO_INVALIDO_DETECTADO", "CLIENTE", clienteId, "No se detecto telefono valido para WhatsApp en importacion.");
            await _notificaciones.CrearAsync("CLIENTE_SIN_TELEFONO_VALIDO", "Cliente sin telefono valido", $"Cliente importado sin telefono valido en fila {row.RowNumber}.", "CLIENTE", clienteId, $"import-phone-invalid:{clienteId}:{row.RowNumber}");
        }
        else
        {
            await _auditoria.RegistrarAsync("TELEFONO_NORMALIZADO", "CLIENTE", clienteId, $"Telefono normalizado a {row.PhoneNormalization.PrincipalWhatsApp}.");
        }

        if (cliente.ReferidoDetectado && !string.IsNullOrWhiteSpace(cliente.ReferidoPorNombre))
            await _auditoria.RegistrarAsync("REFERIDO_DETECTADO", "CLIENTE", clienteId, $"Referido detectado automaticamente: {cliente.ReferidoPorNombre}.");
        if (poliza is null)
        {
            await _auditoria.RegistrarAsync("CLIENTE_IMPORTADO_SIN_POLIZA", "CLIENTE", clienteId, $"Fila {row.RowNumber}: cliente importado sin numero de poliza.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(poliza.EstadoPolizaReal))
            await _auditoria.RegistrarAsync("ESTADO_POLIZA_DETECTADO", "POLIZA", null, $"Fila {row.RowNumber}: {poliza.EstadoPolizaReal}.");
        if (!string.IsNullOrWhiteSpace(poliza.MotivoEstadoPago))
            await _auditoria.RegistrarAsync("ESTADO_PAGO_DETECTADO", "POLIZA", null, $"Fila {row.RowNumber}: {poliza.EstadoPago} ({poliza.MotivoEstadoPago}).");

        if (suma is not null && suma.Parseada)
            await _auditoria.RegistrarAsync("SUMA_ASEGURADA_PARSEADA", "POLIZA", null, $"Fila {row.RowNumber}: suma asegurada procesada.");
        else if (suma is not null && !string.IsNullOrWhiteSpace(suma.TextoOriginal))
            await _auditoria.RegistrarAsync("SUMA_ASEGURADA_FORMATO_INVALIDO", "POLIZA", null, $"Fila {row.RowNumber}: no se pudo parsear '{suma.TextoOriginal}'.");

        if (ramo is not null)
            await _auditoria.RegistrarAsync("RAMO_NORMALIZADO", "POLIZA", null, $"Fila {row.RowNumber}: ramo original '{ramo.Original}' => '{ramo.Normalized}'.");
        if (ramo is not null && ramo.RequiresReview)
            await _auditoria.RegistrarAsync("RAMO_NO_RECONOCIDO", "POLIZA", null, $"Fila {row.RowNumber}: ramo no reconocido '{ramo.Original}'.");
    }

    private static SumaAseguradaMedica ParseSumaAseguradaMedica(string ramo, string textoOriginal, string textoLimpio)
    {
        var original = string.IsNullOrWhiteSpace(textoOriginal) ? textoLimpio : textoOriginal;
        var isMedico = NormalizeHeader(ramo) == "MEDICO";
        if (!isMedico)
            return new SumaAseguradaMedica(original, null, ParseMoney(string.IsNullOrWhiteSpace(textoLimpio) ? original : textoLimpio).Value, true);

        var source = string.IsNullOrWhiteSpace(original) ? textoLimpio : original;
        var parts = (source ?? "")
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseMoney)
            .Where(x => x.Parsed && x.Value.HasValue)
            .Select(x => x.Value!.Value)
            .ToList();

        if (parts.Count == 0)
            return new SumaAseguradaMedica(original, null, null, false);
        if (parts.Count == 1)
            return new SumaAseguradaMedica(original, null, parts[0], true);

        var mayor = parts.Max();
        var menor = parts.Min();
        return new SumaAseguradaMedica(original, mayor, menor, true);
    }

    private static ObservacionClasificada ClassifyObservaciones(string obs1, string obs2)
    {
        var original = string.Join(" | ", new[] { obs1, obs2 }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(original))
            return new ObservacionClasificada("", "GENERAL", null, null);

        var normalized = RemoveDiacritics(original).ToLowerInvariant();
        if (IsBasuraTecnica(normalized))
            return new ObservacionClasificada(original, "BASURA_TECNICA", null, original);

        if (ContainsAny(normalized, "cambio titular", "cambio de titular", "cambio propietario", "cambio de nombre", "cambio asegurado"))
        {
            var split = original.Split('/', 2, StringSplitOptions.TrimEntries);
            var persona = split[0];
            var nota = split.Length > 1 ? split[1] : original;
            return new ObservacionClasificada(original, "CAMBIO_TITULAR", persona, nota);
        }

        if (ContainsAny(normalized, "cancelada", "falta de pago", "vencida", "anulada", "no renovada"))
            return new ObservacionClasificada(original, "ESTADO_POLIZA", null, original);

        if (LooksLikePersonName(original))
            return new ObservacionClasificada(original, "REFERIDO", original, null);

        return new ObservacionClasificada(original, "GENERAL", null, original);
    }

    private static bool IsBasuraTecnica(string normalized)
    {
        return ContainsAny(normalized, "notas de limpieza", "telefonos_invalidos", "correos_invalidos", "advertencias", "contacto:")
               || normalized.Contains("{", StringComparison.Ordinal)
               || normalized.Contains("}", StringComparison.Ordinal)
               || normalized.Contains("[", StringComparison.Ordinal)
               || normalized.Contains("]", StringComparison.Ordinal);
    }

    private static bool LooksLikePersonName(string value)
    {
        if (value.Contains("/", StringComparison.Ordinal))
            return false;
        if (Regex.IsMatch(value, @"[\d@#{}[\]<>:;]"))
            return false;
        var clean = Regex.Replace(value, @"\s+", " ").Trim();
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 1 or > 3)
            return false;
        return words.All(x => Regex.IsMatch(x, @"^[A-Za-zÁÉÍÓÚÑáéíóúñ]+$"));
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(x => value.Contains(x, StringComparison.Ordinal));
    }

    private static string NormalizeRamoRaw(string value)
    {
        var clean = RemoveDiacritics((value ?? "").Trim().ToUpperInvariant());
        clean = Regex.Replace(clean, @"[\t\r\n]+", " ");
        clean = Regex.Replace(clean, @"[^\w\s/\-]", " ");
        clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();
        return clean;
    }

    private static string? BuildClienteRevisionReason(CarteraImportRowPreview row, RamoParseResult ramo, SumaAseguradaMedica suma)
    {
        var reasons = new List<string>();
        if (!row.PhoneNormalization.WhatsappReady)
            reasons.Add("Cliente sin telefono valido");
        if (ramo.RequiresReview)
            reasons.Add(ramo.Reason ?? "Ramo no reconocido");
        if (!suma.Parseada && !string.IsNullOrWhiteSpace(suma.TextoOriginal))
            reasons.Add("Suma asegurada no parseable");
        return reasons.Count == 0 ? null : string.Join("; ", reasons);
    }

    private static string? BuildPolizaRevisionReason(RamoParseResult ramo, SumaAseguradaMedica suma, CarteraImportRowPreview row)
    {
        var reasons = new List<string>();
        if (ramo.RequiresReview)
            reasons.Add(ramo.Reason ?? "Ramo no reconocido");
        if (!suma.Parseada && !string.IsNullOrWhiteSpace(suma.TextoOriginal))
            reasons.Add("Formato suma asegurada invalido");
        if (!row.PhoneNormalization.WhatsappReady)
            reasons.Add("Cliente sin telefono valido");
        return reasons.Count == 0 ? null : string.Join("; ", reasons);
    }

    private static RamoParseResult ParseRamo(string value)
    {
        var original = value?.Trim() ?? "";
        var normalizedRaw = NormalizeRamoRaw(original);
        var key = NormalizeHeader(normalizedRaw);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACCIDENTESPERSONALES"] = "ACCIDENTES_PERSONALES",
            ["ACCIDENTEPERSONAL"] = "ACCIDENTES_PERSONALES",
            ["ACCIDENTESPERSONAL"] = "ACCIDENTES_PERSONALES",
            ["AP"] = "ACCIDENTES_PERSONALES",
            ["AUTO"] = "AUTOS",
            ["AUTOS"] = "AUTOS",
            ["VEHICULO"] = "AUTOS",
            ["VEHICULOAUTOMOTOR"] = "AUTOS",
            ["MOTO"] = "MOTOS",
            ["MOTOS"] = "MOTOS",
            ["VIDA"] = "VIDA",
            ["EQUIPOYMAQUINARIA"] = "EQUIPO_MAQUINARIA",
            ["EQUIPOMAQUINARIA"] = "EQUIPO_MAQUINARIA",
            ["MAQUINARIA"] = "EQUIPO_MAQUINARIA",
            ["MEDICO"] = "MEDICO",
            ["VIDA2000"] = "VIDA2000",
            ["VIDAMEDICO"] = "VIDA_MEDICO",
            ["RC"] = "RC",
            ["RESPONSABILIDADCIVIL"] = "RC",
            ["INCENDIO"] = "INCENDIO",
            ["INCENCIO"] = "INCENDIO",
            ["INCENCIDO"] = "INCENDIO"
        };
        if (map.TryGetValue(key, out var normalized))
            return new RamoParseResult(original, normalized, false, null, null);

        if (normalizedRaw is "ACCIDENTES PERSONALES" or "ACCIDENTE PERSONAL" or "ACCIDENTES PERSONAL")
            return new RamoParseResult(original, "ACCIDENTES_PERSONALES", false, null, null);
        if (normalizedRaw is "VIDA/MEDICO" or "VIDA MEDICO" or "VIDA-MEDICO")
            return new RamoParseResult(original, "VIDA_MEDICO", false, null, null);
        if (normalizedRaw is "EQUIPO Y MAQUINARIA" or "EQUIPO MAQUINARIA")
            return new RamoParseResult(original, "EQUIPO_MAQUINARIA", false, null, null);
        if (normalizedRaw is "VIDA 2000")
            return new RamoParseResult(original, "VIDA2000", false, null, null);
        if (normalizedRaw is "RESPONSABILIDAD CIVIL")
            return new RamoParseResult(original, "RC", false, null, null);

        return new RamoParseResult(original, "OTROS", true, $"Ramo no reconocido: {original}", "{\"ramo_original\":\"" + original.Replace("\"", "\\\"") + "\"}");
    }

    private static EmisionParseResult ParseEmisionRenovacion(string emisionRaw, string observacion2Raw)
    {
        var raw = (emisionRaw ?? "").Trim();
        var normalized = RemoveDiacritics(raw).ToUpperInvariant();
        string? tipoProceso = null;
        if (normalized.Contains("EMISION", StringComparison.Ordinal))
            tipoProceso = "EMISION";
        else if (normalized.Contains("RENOVACION", StringComparison.Ordinal))
            tipoProceso = "RENOVACION";

        var obs2 = RemoveDiacritics((observacion2Raw ?? "").ToUpperInvariant());
        string? estado = null;
        if (ContainsAny(obs2, "CANCELADA", "NUNCA PAGO", "PERDIDA TOTAL"))
            estado = "CANCELADA";
        else if (ContainsAny(obs2, "NO RENOVO", "NO RENOVARA"))
            estado = "NO_RENOVADA";
        else if (ContainsAny(obs2, "EN PROCESO DE CANCELACION"))
            estado = "PENDIENTE";

        string? motivo = null;
        var motivos = new[]
        {
            "falta de pago","incremento en prima","cambio de vehiculo","vendio el vehiculo","paso a otra compania",
            "paso a atlantida","paso a banpais","no contesta","se fue del pais","cambio de agente","perdida total","cambio de titular"
        };
        var obs2Min = RemoveDiacritics((observacion2Raw ?? "").ToLowerInvariant());
        motivo = motivos.FirstOrDefault(m => obs2Min.Contains(m, StringComparison.Ordinal));

        return new EmisionParseResult(raw, tipoProceso, estado, motivo);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string CleanText(string value) => string.IsNullOrWhiteSpace(value) ? "" : Regex.Replace(value.Trim(), @"\s+", " ");
    private static string NormalizeKey(string value) => RemoveDiacritics(CleanText(value)).ToUpperInvariant();
    private static string NormalizeHeader(string value) => Regex.Replace(NormalizeKey(value), @"[^A-Z0-9]", "");
    private static bool SameField(string left, string right) => string.Equals(NormalizeHeader(left), NormalizeHeader(right), StringComparison.OrdinalIgnoreCase);
    private static bool IsPlaceholderClientName(string? value)
    {
        var normalized = NormalizeHeader(value ?? "");
        return normalized is "CLIENTE" or "CLIENTE2" or "NOMBRE" or "ASEGURADO";
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(NormalizationForm.FormC)
            .Replace("Ã", "A").Replace("Ã‰", "E").Replace("Ã", "I").Replace("Ã“", "O").Replace("Ãš", "U").Replace("Ã‘", "N")
            .Replace("ÃƒÂ", "A").Replace("Ãƒâ€°", "E").Replace("ÃƒÂ", "I").Replace("Ãƒâ€œ", "O").Replace("ÃƒÅ¡", "U").Replace("Ãƒâ€˜", "N")
            .Replace("CUMPLEAÃƒâ€˜OS", "CUMPLEANOS").Replace("COMPAÃƒâ€˜IA", "COMPANIA");
    }
}

public sealed class CarteraImportPreview
{
    public List<CarteraImportRowPreview> Rows { get; set; } = [];
    public int TotalRows { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public bool HasCriticalErrors => ErrorCount > 0;
}

public sealed class CarteraImportResult
{
    public int Clientes { get; set; }
    public int Polizas { get; set; }
    public int PolizasDuplicadas { get; set; }
    public int FilasImportadas { get; set; }
    public int FilasRechazadas { get; set; }
}

public sealed class CarteraImportRowPreview
{
    public int RowNumber { get; set; }
    public List<CarteraImportFieldValue> Values { get; set; } = [];
    public List<CarteraImportIssue> Errors { get; set; } = [];
    public List<CarteraImportIssue> Warnings { get; set; } = [];
    public PhoneNormalizationResult PhoneNormalization { get; set; } = new();
    public EmailNormalization EmailNormalization { get; set; } = new();
    public bool IsEmpty => Values.All(x => string.IsNullOrWhiteSpace(x.Original));
    public string Estado => Errors.Count > 0 ? "ERROR" : Warnings.Count > 0 ? "CON_ADVERTENCIAS" : "VALIDA";
    public string GetOriginal(string field) => Values.FirstOrDefault(x => SameField(x.Field, field))?.Original ?? "";
    public string GetClean(string field) => Values.FirstOrDefault(x => SameField(x.Field, field))?.Clean ?? "";
    public void SetClean(string field, string value)
    {
        var index = Values.FindIndex(x => SameField(x.Field, field));
        if (index >= 0)
            Values[index] = Values[index] with { Clean = value };
    }
    private static bool SameField(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value) => Regex.Replace((value ?? "").Trim().ToUpperInvariant()
        .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N")
        .Replace("CUMPLEAÑOS", "CUMPLEANOS").Replace("COMPAÑIA", "COMPANIA"), @"[^A-Z0-9]", "");
}

public sealed record CarteraImportFieldValue(string Field, string Original, string Clean);
public sealed record CarteraImportIssue(string Field, string Message);
public sealed record MoneyParseResult(decimal? Value, bool Parsed, bool MultipleValues);
public sealed record SumaAseguradaMedica(string TextoOriginal, decimal? MaximoVitalicio, decimal? SumaAseguradaVida, bool Parseada);
public sealed record ObservacionClasificada(string Original, string Tipo, string? PersonaRelacionada, string? NotaAdministrativa);
public sealed record RamoParseResult(string Original, string Normalized, bool RequiresReview, string? Reason, string? ExtrasJson);
public sealed record EmisionParseResult(string Raw, string? TipoProceso, string? EstadoPolizaReal, string? MotivoCancelacion);

public sealed class EmailNormalization
{
    public string Original { get; set; } = "";
    public List<string> Valid { get; set; } = [];
    public List<string> Invalid { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public string Principal => Valid.FirstOrDefault() ?? "";
    public IReadOnlyCollection<string> Extras => Valid.Skip(1).ToList();
}
