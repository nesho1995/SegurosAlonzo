CREATE TABLE IF NOT EXISTS talleres (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(200) NOT NULL,
    ciudad VARCHAR(120) NOT NULL,
    aseguradora VARCHAR(160) NOT NULL,
    ramo VARCHAR(120) NULL,
    telefono VARCHAR(60) NULL,
    direccion VARCHAR(500) NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_talleres_busqueda (ciudad, aseguradora, ramo),
    INDEX ix_talleres_activo (activo)
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
);

INSERT INTO app_settings (setting_group, setting_key, setting_value)
VALUES
('extractor_avanzado', 'PalabrasClaveAsunto', 'reclamo,siniestro'),
('extractor_avanzado', 'CamposObligatorios', 'Asegurado,Poliza,Placa,Reclamo,Conductor,Celular'),
('extractor_avanzado', 'PlantillaWhatsApp', 'Estimado {Conductor}, hemos recibido el reclamo {Reclamo} de la poliza {Poliza}. Le estaremos dando seguimiento.')
ON DUPLICATE KEY UPDATE setting_value = setting_value;
