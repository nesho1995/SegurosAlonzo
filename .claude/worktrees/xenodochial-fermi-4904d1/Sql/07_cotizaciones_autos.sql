-- OBSOLETO desde 2026-04-28:
-- El modulo Comparador de Cotizaciones fue deshabilitado en API y React.
-- Estas tablas se conservan solo para retencion historica; no ejecutar DROP
-- ni borrar datos sin una migracion aprobada de archivo/retencion.

CREATE TABLE IF NOT EXISTS cotizacion_oportunidades (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cliente_id INT NULL,
    nombre_cliente VARCHAR(200) NOT NULL,
    tipo_seguro VARCHAR(30) NOT NULL DEFAULT 'AUTO',
    estado VARCHAR(30) NOT NULL DEFAULT 'BORRADOR',
    recomendacion_general TEXT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    usuario_id INT NULL,
    INDEX idx_cot_oportunidades_estado (estado),
    INDEX idx_cot_oportunidades_cliente (cliente_id)
);

CREATE TABLE IF NOT EXISTS cotizacion_archivos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    oportunidad_id INT NOT NULL,
    aseguradora VARCHAR(60) NULL,
    nombre_archivo VARCHAR(255) NOT NULL,
    ruta_archivo VARCHAR(600) NOT NULL,
    tamaño BIGINT NOT NULL,
    extension VARCHAR(20) NOT NULL,
    estado_extraccion VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    error_extraccion TEXT NULL,
    fecha_subida DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_cot_archivos_oportunidad (oportunidad_id),
    CONSTRAINT fk_cot_archivos_oportunidad FOREIGN KEY (oportunidad_id) REFERENCES cotizacion_oportunidades(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS cotizacion_datos_extraidos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    archivo_id INT NOT NULL,
    oportunidad_id INT NOT NULL,
    aseguradora VARCHAR(60) NOT NULL,
    cliente VARCHAR(200) NULL,
    marca VARCHAR(120) NULL,
    modelo VARCHAR(120) NULL,
    tipo VARCHAR(120) NULL,
    anio INT NULL,
    suma_asegurada DECIMAL(18,2) NULL,
    prima_neta DECIMAL(18,2) NULL,
    descuento DECIMAL(18,2) NULL,
    isv DECIMAL(18,2) NULL,
    gastos_emision DECIMAL(18,2) NULL,
    prima_total DECIMAL(18,2) NULL,
    pago_contado DECIMAL(18,2) NULL,
    descuento_contado DECIMAL(18,2) NULL,
    numero_cuotas INT NULL,
    primera_cuota DECIMAL(18,2) NULL,
    cuotas_siguientes DECIMAL(18,2) NULL,
    rc_bienes DECIMAL(18,2) NULL,
    rc_personas DECIMAL(18,2) NULL,
    gastos_medicos DECIMAL(18,2) NULL,
    ocupantes DECIMAL(18,2) NULL,
    extension_territorial VARCHAR(200) NULL,
    deducible_colision_porcentaje DECIMAL(8,4) NULL,
    deducible_colision_monto DECIMAL(18,2) NULL,
    coaseguro_robo_porcentaje DECIMAL(8,4) NULL,
    coaseguro_robo_monto_cliente DECIMAL(18,2) NULL,
    coaseguro_perdida_total_porcentaje DECIMAL(8,4) NULL,
    coaseguro_perdida_total_monto_cliente DECIMAL(18,2) NULL,
    rotura_cristales VARCHAR(200) NULL,
    beneficios_json JSON NULL,
    alertas_json JSON NULL,
    raw_json JSON NULL,
    confianza DECIMAL(5,2) NOT NULL DEFAULT 0,
    fecha_extraccion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_cot_datos_archivo (archivo_id),
    INDEX idx_cot_datos_oportunidad (oportunidad_id),
    CONSTRAINT fk_cot_datos_archivo FOREIGN KEY (archivo_id) REFERENCES cotizacion_archivos(id) ON DELETE CASCADE,
    CONSTRAINT fk_cot_datos_oportunidad FOREIGN KEY (oportunidad_id) REFERENCES cotizacion_oportunidades(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS cotizacion_mapeos_aseguradora (
    id INT AUTO_INCREMENT PRIMARY KEY,
    aseguradora VARCHAR(60) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    version INT NOT NULL DEFAULT 1,
    reglas_json JSON NOT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_cot_mapeos_aseguradora (aseguradora, activo)
);

CREATE TABLE IF NOT EXISTS cotizacion_comparativos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    oportunidad_id INT NOT NULL,
    resumen_ejecutivo MEDIUMTEXT NOT NULL,
    mejor_precio_financiado VARCHAR(60) NULL,
    mejor_precio_contado VARCHAR(60) NULL,
    mejor_cobertura VARCHAR(60) NULL,
    mejor_balance VARCHAR(60) NULL,
    alertas_json JSON NULL,
    ruta_pdf_generado VARCHAR(600) NULL,
    fecha_generacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_cot_comparativos_oportunidad (oportunidad_id),
    CONSTRAINT fk_cot_comparativos_oportunidad FOREIGN KEY (oportunidad_id) REFERENCES cotizacion_oportunidades(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS cotizacion_envios (
    id INT AUTO_INCREMENT PRIMARY KEY,
    oportunidad_id INT NOT NULL,
    canal VARCHAR(30) NOT NULL,
    destinatario VARCHAR(200) NULL,
    mensaje MEDIUMTEXT NULL,
    fecha_envio DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado VARCHAR(30) NOT NULL DEFAULT 'PREPARADO',
    respuesta_json JSON NULL,
    INDEX idx_cot_envios_oportunidad (oportunidad_id),
    CONSTRAINT fk_cot_envios_oportunidad FOREIGN KEY (oportunidad_id) REFERENCES cotizacion_oportunidades(id) ON DELETE CASCADE
);
