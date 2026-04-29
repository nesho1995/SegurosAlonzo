USE reclamos_auto;

CREATE TABLE IF NOT EXISTS empresa_configuracion (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nombre_empresa VARCHAR(180) NOT NULL DEFAULT 'Seguros Alonzo',
    logo_ruta VARCHAR(500) NULL,
    color_primario VARCHAR(20) NULL,
    fecha_actualizacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    usuario_actualizacion_id INT NULL
);

INSERT INTO empresa_configuracion (id, nombre_empresa, color_primario)
VALUES (1, 'Seguros Alonzo', '#2563eb')
ON DUPLICATE KEY UPDATE nombre_empresa = nombre_empresa;

ALTER TABLE clientes
    ADD COLUMN IF NOT EXISTS notas_calidad_json LONGTEXT NULL AFTER observaciones,
    ADD COLUMN IF NOT EXISTS datos_revisados TINYINT(1) NOT NULL DEFAULT 0 AFTER notas_calidad_json,
    ADD COLUMN IF NOT EXISTS fecha_revision DATETIME NULL AFTER datos_revisados,
    ADD COLUMN IF NOT EXISTS usuario_revision_id INT NULL AFTER fecha_revision;

ALTER TABLE polizas
    ADD COLUMN IF NOT EXISTS notas_calidad_json LONGTEXT NULL AFTER observaciones,
    ADD COLUMN IF NOT EXISTS datos_revisados TINYINT(1) NOT NULL DEFAULT 0 AFTER notas_calidad_json,
    ADD COLUMN IF NOT EXISTS fecha_revision DATETIME NULL AFTER datos_revisados,
    ADD COLUMN IF NOT EXISTS usuario_revision_id INT NULL AFTER fecha_revision;

CREATE TABLE IF NOT EXISTS gastos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    fecha DATE NOT NULL,
    categoria VARCHAR(80) NOT NULL,
    descripcion VARCHAR(500) NOT NULL,
    proveedor VARCHAR(180) NULL,
    monto DECIMAL(18,2) NOT NULL,
    moneda VARCHAR(10) NOT NULL DEFAULT 'HNL',
    metodo_pago VARCHAR(80) NULL,
    referencia VARCHAR(160) NULL,
    documento_id INT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'REGISTRADO',
    creado_por INT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    INDEX ix_gastos_fecha (fecha),
    INDEX ix_gastos_categoria (categoria),
    INDEX ix_gastos_estado (estado)
);

CREATE TABLE IF NOT EXISTS comisiones_lotes (
    id INT AUTO_INCREMENT PRIMARY KEY,
    aseguradora VARCHAR(160) NULL,
    archivo_nombre VARCHAR(255) NOT NULL,
    fecha_carga DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    usuario_id INT NULL,
    estado VARCHAR(40) NOT NULL DEFAULT 'EN_REVISION'
);

CREATE TABLE IF NOT EXISTS comisiones_detalle (
    id INT AUTO_INCREMENT PRIMARY KEY,
    lote_id INT NOT NULL,
    poliza_id INT NULL,
    cliente_detectado VARCHAR(200) NULL,
    poliza_detectada VARCHAR(120) NULL,
    aseguradora_detectada VARCHAR(160) NULL,
    prima_detectada DECIMAL(18,2) NULL,
    porcentaje_detectado DECIMAL(8,4) NULL,
    comision_detectada DECIMAL(18,2) NULL,
    comision_esperada DECIMAL(18,2) NULL,
    diferencia DECIMAL(18,2) NULL,
    fecha_pago DATE NULL,
    referencia VARCHAR(160) NULL,
    estado VARCHAR(60) NOT NULL,
    observaciones VARCHAR(1000) NULL,
    revisado TINYINT(1) NOT NULL DEFAULT 0,
    fecha_revision DATETIME NULL,
    usuario_revision_id INT NULL,
    CONSTRAINT fk_comisiones_detalle_lote FOREIGN KEY (lote_id)
        REFERENCES comisiones_lotes(id) ON DELETE CASCADE,
    INDEX ix_comisiones_detalle_estado (estado),
    INDEX ix_comisiones_detalle_poliza (poliza_detectada)
);
