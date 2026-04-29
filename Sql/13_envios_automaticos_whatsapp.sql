USE reclamos_auto;

CREATE TABLE IF NOT EXISTS whatsapp_envios_log (
    id INT AUTO_INCREMENT PRIMARY KEY,
    referencia VARCHAR(180) NOT NULL,
    entidad_tipo VARCHAR(60) NOT NULL,
    entidad_id INT NULL,
    telefono VARCHAR(50) NULL,
    tipo_evento VARCHAR(80) NOT NULL,
    automatico BOOLEAN NOT NULL DEFAULT TRUE,
    estado VARCHAR(30) NOT NULL,
    mensaje TEXT NULL,
    respuesta TEXT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_whatsapp_envios_referencia (referencia),
    INDEX ix_whatsapp_envios_entidad (entidad_tipo, entidad_id),
    INDEX ix_whatsapp_envios_estado (estado)
);

INSERT INTO app_settings (setting_group, setting_key, setting_value)
VALUES
('envios_automaticos', 'AutoEnviarReclamos', 'false'),
('envios_automaticos', 'AutoEnviarRecordatoriosPago', 'false'),
('envios_automaticos', 'AutoEnviarRecordatoriosPoliza', 'false'),
('envios_automaticos', 'DiasAntesVencimientoCuota', '7,3,1'),
('envios_automaticos', 'DiasDespuesCuotaVencida', '1,3,7,15'),
('envios_automaticos', 'DiasAntesVencimientoPoliza', '30,15,7'),
('envios_automaticos', 'PlantillaPagoProximo', 'Estimado(a) {cliente}, le recordamos que la cuota de su poliza {poliza} vence el {fecha_vencimiento} por un monto de {monto}.'),
('envios_automaticos', 'PlantillaPagoVencido', 'Estimado(a) {cliente}, su cuota de la poliza {poliza} se encuentra vencida desde el {fecha_vencimiento} por un monto de {monto}.'),
('envios_automaticos', 'PlantillaPolizaPorVencer', 'Estimado(a) {cliente}, su poliza {poliza} de {aseguradora} vence el {fecha_vencimiento}. Podemos apoyarle con la renovacion.'),
('envios_automaticos', 'PlantillaPolizaVencida', 'Estimado(a) {cliente}, su poliza {poliza} vencio el {fecha_vencimiento}. Podemos apoyarle con su renovacion.'),
('envios_automaticos', 'PlantillaReclamo', 'Estimado(a) {cliente}, hemos recibido el reclamo {reclamo} de la poliza {poliza}. Le estaremos dando seguimiento.')
ON DUPLICATE KEY UPDATE setting_value = setting_value;
