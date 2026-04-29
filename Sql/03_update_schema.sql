USE reclamos_auto;

ALTER TABLE reclamos_whatsapp
    ADD COLUMN IF NOT EXISTS fecha_ultimo_recordatorio DATETIME NULL,
    ADD COLUMN IF NOT EXISTS cantidad_recordatorios INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cliente_id INT NULL,
    ADD COLUMN IF NOT EXISTS poliza_id INT NULL,
    ADD COLUMN IF NOT EXISTS numero_reclamo VARCHAR(80) NULL,
    ADD COLUMN IF NOT EXISTS fecha_reclamo DATE NULL,
    ADD COLUMN IF NOT EXISTS tipo_reclamo VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS estado_reclamo VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS taller_sugerido_id INT NULL,
    ADD COLUMN IF NOT EXISTS taller_asignado_id INT NULL,
    ADD COLUMN IF NOT EXISTS descripcion TEXT NULL,
    ADD COLUMN IF NOT EXISTS monto_estimado DECIMAL(18,2) NULL,
    ADD COLUMN IF NOT EXISTS monto_aprobado DECIMAL(18,2) NULL,
    ADD COLUMN IF NOT EXISTS monto_pagado DECIMAL(18,2) NULL,
    ADD COLUMN IF NOT EXISTS actualizado_en DATETIME NULL;

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS IsActive TINYINT(1) NOT NULL DEFAULT 1;

CREATE TABLE IF NOT EXISTS reclamo_documentos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    reclamo_id INT NOT NULL,
    documento VARCHAR(200) NOT NULL,
    recibido TINYINT(1) NOT NULL DEFAULT 0,
    fecha_recibido DATETIME NULL,
    CONSTRAINT fk_reclamo_documentos_reclamo
        FOREIGN KEY (reclamo_id) REFERENCES reclamos_whatsapp(id)
        ON DELETE CASCADE,
    INDEX ix_reclamo_documentos_reclamo_id (reclamo_id)
);

CREATE TABLE IF NOT EXISTS clientes (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    telefono VARCHAR(50) NULL,
    contacto VARCHAR(200) NULL,
    email VARCHAR(150) NULL,
    identidad VARCHAR(50) NULL,
    fecha_nacimiento DATE NULL,
    ciudad VARCHAR(100) NULL,
    observaciones TEXT NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_clientes_nombre (nombre)
);

CREATE TABLE IF NOT EXISTS cliente_telefonos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cliente_id INT NOT NULL,
    telefono VARCHAR(30) NOT NULL,
    tipo VARCHAR(30) NOT NULL DEFAULT 'GENERAL',
    principal TINYINT(1) NOT NULL DEFAULT 0,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_cliente_telefonos_cliente
        FOREIGN KEY (cliente_id) REFERENCES clientes(id)
        ON DELETE CASCADE,
    UNIQUE KEY uq_cliente_telefonos_cliente_telefono (cliente_id, telefono),
    INDEX ix_cliente_telefonos_cliente_id (cliente_id)
);

CREATE TABLE IF NOT EXISTS polizas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cliente_id INT NOT NULL,
    aseguradora VARCHAR(100) NULL,
    ramo VARCHAR(50) NULL,
    cuotas INT NULL,
    forma_pago VARCHAR(50) NULL,
    numero_poliza VARCHAR(100) NULL,
    certificado VARCHAR(100) NULL,
    endoso VARCHAR(100) NULL,
    prima_neta DECIMAL(18,2) NULL,
    seguro_asiento DECIMAL(18,2) NULL,
    prima_comercial DECIMAL(18,2) NULL,
    impuesto DECIMAL(18,2) NULL,
    gastos_emision DECIMAL(18,2) NULL,
    bomberos DECIMAL(18,2) NULL,
    prima_total DECIMAL(18,2) NULL,
    plan VARCHAR(100) NULL,
    suma_asegurada DECIMAL(18,2) NULL,
    vigencia DATE NULL,
    hasta DATE NULL,
    medio VARCHAR(100) NULL,
    vehiculo VARCHAR(150) NULL,
    emision_renovacion VARCHAR(50) NULL,
    estado_pago VARCHAR(50) NOT NULL DEFAULT 'SIN_VALIDAR',
    observaciones TEXT NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    fecha_inicio DATE NULL,
    fecha_fin DATE NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_polizas_cliente
        FOREIGN KEY (cliente_id) REFERENCES clientes(id)
        ON DELETE CASCADE,
    INDEX ix_polizas_cliente_id (cliente_id),
    INDEX ix_polizas_numero_poliza (numero_poliza)
);

ALTER TABLE polizas
    ADD COLUMN IF NOT EXISTS activo TINYINT(1) NOT NULL DEFAULT 1;

ALTER TABLE clientes
    ADD COLUMN IF NOT EXISTS referido_por_nombre VARCHAR(180) NULL,
    ADD COLUMN IF NOT EXISTS referido_detectado TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requiere_revision_manual TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS estado_revision VARCHAR(40) NOT NULL DEFAULT 'OK',
    ADD COLUMN IF NOT EXISTS motivo_revision TEXT NULL;

ALTER TABLE documentos
    ADD COLUMN IF NOT EXISTS nombre_archivo_original VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS nombre_archivo_guardado VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS ruta_relativa VARCHAR(500) NULL,
    ADD COLUMN IF NOT EXISTS ruta_archivo VARCHAR(500) NULL,
    ADD COLUMN IF NOT EXISTS mime_type VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
    ADD COLUMN IF NOT EXISTS tamano_bytes BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS hash_archivo VARCHAR(128) NULL,
    ADD COLUMN IF NOT EXISTS subido_por_usuario_id INT NULL,
    ADD COLUMN IF NOT EXISTS activo TINYINT(1) NOT NULL DEFAULT 1;

ALTER TABLE poliza_cuotas
    ADD COLUMN IF NOT EXISTS documento_id INT NULL,
    ADD COLUMN IF NOT EXISTS numero_recibo VARCHAR(80) NULL,
    ADD COLUMN IF NOT EXISTS referencia_banco VARCHAR(120) NULL;

CREATE TABLE IF NOT EXISTS poliza_pagos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cuota_id INT NOT NULL,
    monto DECIMAL(18,2) NOT NULL,
    fecha_pago DATE NOT NULL,
    metodo_pago VARCHAR(40) NOT NULL DEFAULT 'OTRO',
    documento_id INT NULL,
    numero_recibo VARCHAR(80) NULL,
    referencia_banco VARCHAR(120) NULL,
    observaciones TEXT NULL,
    registrado_por_usuario_id INT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    INDEX ix_poliza_pagos_cuota (cuota_id),
    INDEX ix_poliza_pagos_fecha (fecha_pago)
);

CREATE TABLE IF NOT EXISTS reclamo_requisitos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tipo_reclamo VARCHAR(40) NOT NULL,
    tipo_documento VARCHAR(80) NOT NULL,
    requerido TINYINT(1) NOT NULL DEFAULT 1,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    UNIQUE KEY uq_reclamo_requisito (tipo_reclamo, tipo_documento)
);

ALTER TABLE polizas
    ADD COLUMN IF NOT EXISTS ramo_normalizado VARCHAR(60) NULL,
    ADD COLUMN IF NOT EXISTS extras_json TEXT NULL,
    ADD COLUMN IF NOT EXISTS suma_asegurada_texto_original VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS maximo_vitalicio DECIMAL(18,2) NULL,
    ADD COLUMN IF NOT EXISTS suma_asegurada_vida DECIMAL(18,2) NULL,
    ADD COLUMN IF NOT EXISTS observacion2 TEXT NULL,
    ADD COLUMN IF NOT EXISTS tipo_proceso VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS estado_poliza_real VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS motivo_cancelacion TEXT NULL,
    ADD COLUMN IF NOT EXISTS motivo_estado_pago VARCHAR(120) NULL,
    ADD COLUMN IF NOT EXISTS origen_ramo_normalizado VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS origen_tipo_proceso VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS origen_estado_poliza_real VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS origen_estado_pago VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS origen_suma_asegurada VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS observacion_original TEXT NULL,
    ADD COLUMN IF NOT EXISTS observacion_tipo VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS persona_relacionada VARCHAR(180) NULL,
    ADD COLUMN IF NOT EXISTS nota_administrativa TEXT NULL,
    ADD COLUMN IF NOT EXISTS requiere_revision_manual TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS estado_revision VARCHAR(40) NOT NULL DEFAULT 'OK',
    ADD COLUMN IF NOT EXISTS motivo_revision TEXT NULL;

CREATE TABLE IF NOT EXISTS app_settings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    setting_group VARCHAR(80) NOT NULL,
    setting_key VARCHAR(120) NOT NULL,
    setting_value TEXT NOT NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_app_settings_group_key (setting_group, setting_key)
);

INSERT INTO app_settings (setting_group, setting_key, setting_value)
VALUES
('correo_extractor', 'SubjectPattern', '(?<placa>[A-Z]{2,4}-?[0-9]{3,4})\\s*,\\s*(?<reclamo>[A-Z]{2,5}-[0-9]{2,5}-[0-9]{4})'),
('correo_extractor', 'AseguradoPattern', 'Asegurado\\s*:\\s*(.+)'),
('correo_extractor', 'PolizaPattern', '(?:No\\.\\s*)?P[oó]liza\\s*:\\s*(.+)'),
('correo_extractor', 'CaracteristicasPattern', 'Caracter[ií]sticas del Bien asegurado\\s*:\\s*(.+)'),
('correo_extractor', 'ConductorPattern', 'Conductor\\s*:\\s*(.+)'),
('correo_extractor', 'CelularPattern', 'Celular\\s*:\\s*([0-9\\-\\s\\+]+)'),
('correo_extractor', 'FechaPattern', 'notificado con fecha\\s*([0-9]{1,2}/[0-9]{1,2}/[0-9]{4})'),
('correo_extractor', 'LugarPattern', 'Ocurrido en\\s*(.+?)(?:\\.|\\r|\\n)')
ON DUPLICATE KEY UPDATE setting_value = setting_value;

CREATE TABLE IF NOT EXISTS recordatorios (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tipo VARCHAR(30) NOT NULL,
    referencia VARCHAR(60) NOT NULL,
    cliente_id INT NOT NULL,
    poliza_id INT NULL,
    telefono VARCHAR(30) NULL,
    fecha_objetivo DATE NULL,
    asunto VARCHAR(200) NOT NULL,
    mensaje TEXT NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_envio DATETIME NULL,
    error TEXT NULL,
    CONSTRAINT fk_recordatorios_cliente
        FOREIGN KEY (cliente_id) REFERENCES clientes(id)
        ON DELETE CASCADE,
    CONSTRAINT fk_recordatorios_poliza
        FOREIGN KEY (poliza_id) REFERENCES polizas(id)
        ON DELETE SET NULL,
    UNIQUE KEY uq_recordatorios_tipo_ref_poliza (tipo, referencia, poliza_id),
    INDEX ix_recordatorios_estado (estado),
    INDEX ix_recordatorios_cliente_id (cliente_id)
);

CREATE TABLE IF NOT EXISTS poliza_cuotas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    poliza_id INT NOT NULL,
    numero_cuota INT NOT NULL,
    fecha_vencimiento DATE NOT NULL,
    monto DECIMAL(18,2) NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    fecha_pago DATE NULL,
    comprobante_url VARCHAR(500) NULL,
    observaciones TEXT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_poliza_cuotas_poliza
        FOREIGN KEY (poliza_id) REFERENCES polizas(id)
        ON DELETE CASCADE,
    UNIQUE KEY uq_poliza_cuotas_poliza_numero (poliza_id, numero_cuota),
    INDEX ix_poliza_cuotas_estado (estado),
    INDEX ix_poliza_cuotas_vencimiento (fecha_vencimiento)
);

ALTER TABLE recordatorios
    ADD COLUMN IF NOT EXISTS cuota_id INT NULL;

UPDATE recordatorios
SET estado = 'DESCARTADO'
WHERE tipo = 'PAGO'
  AND referencia = 'PAGO_REVISION'
  AND estado = 'PENDIENTE';
