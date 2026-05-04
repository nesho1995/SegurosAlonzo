USE reclamos_auto;

CREATE TABLE IF NOT EXISTS catalogos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tipo_catalogo VARCHAR(60) NOT NULL,
    codigo VARCHAR(120) NOT NULL,
    nombre VARCHAR(180) NOT NULL,
    descripcion TEXT NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    orden INT NOT NULL DEFAULT 0,
    es_default TINYINT(1) NOT NULL DEFAULT 0,
    pendiente_revision TINYINT(1) NOT NULL DEFAULT 0,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_catalogos_tipo_codigo (tipo_catalogo, codigo),
    INDEX ix_catalogos_tipo_activo_orden (tipo_catalogo, activo, orden)
);

ALTER TABLE clientes
    ADD INDEX ix_clientes_telefono (telefono),
    ADD INDEX ix_clientes_email (email),
    ADD INDEX ix_clientes_referido (referido_por_nombre),
    ADD INDEX ix_clientes_revision (estado_revision, requiere_revision_manual);

ALTER TABLE polizas
    ADD INDEX ix_polizas_ramo (ramo),
    ADD INDEX ix_polizas_ramo_normalizado (ramo_normalizado),
    ADD INDEX ix_polizas_aseguradora (aseguradora),
    ADD INDEX ix_polizas_estado_revision (estado_revision, requiere_revision_manual),
    ADD INDEX ix_polizas_estado_real (estado_poliza_real),
    ADD INDEX ix_polizas_tipo_proceso (tipo_proceso),
    ADD INDEX ix_polizas_vigencia (vigencia),
    ADD INDEX ix_polizas_hasta (hasta),
    ADD INDEX ix_polizas_emision_renovacion (emision_renovacion);
