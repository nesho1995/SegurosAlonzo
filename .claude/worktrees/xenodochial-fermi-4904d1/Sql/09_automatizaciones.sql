CREATE TABLE IF NOT EXISTS automatizaciones (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(160) NOT NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    tipo_evento VARCHAR(80) NOT NULL,
    empresa_id INT NULL,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_automatizaciones_evento (tipo_evento, activo),
    INDEX ix_automatizaciones_empresa (empresa_id)
);

CREATE TABLE IF NOT EXISTS automatizacion_condiciones (
    id INT AUTO_INCREMENT PRIMARY KEY,
    automatizacion_id INT NOT NULL,
    campo VARCHAR(120) NOT NULL,
    operador VARCHAR(30) NOT NULL,
    valor VARCHAR(500) NULL,
    INDEX ix_auto_condiciones_auto (automatizacion_id),
    CONSTRAINT fk_auto_condiciones_auto FOREIGN KEY (automatizacion_id)
        REFERENCES automatizaciones(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS automatizacion_acciones (
    id INT AUTO_INCREMENT PRIMARY KEY,
    automatizacion_id INT NOT NULL,
    tipo_accion VARCHAR(80) NOT NULL,
    parametros_json LONGTEXT NULL,
    INDEX ix_auto_acciones_auto (automatizacion_id),
    CONSTRAINT fk_auto_acciones_auto FOREIGN KEY (automatizacion_id)
        REFERENCES automatizaciones(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS automatizacion_logs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    automatizacion_id INT NOT NULL,
    entidad_tipo VARCHAR(60) NOT NULL,
    entidad_id INT NULL,
    resultado VARCHAR(40) NOT NULL,
    mensaje VARCHAR(1000) NOT NULL,
    fecha DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_auto_logs_auto (automatizacion_id),
    INDEX ix_auto_logs_entidad (entidad_tipo, entidad_id),
    INDEX ix_auto_logs_fecha (fecha),
    CONSTRAINT fk_auto_logs_auto FOREIGN KEY (automatizacion_id)
        REFERENCES automatizaciones(id) ON DELETE CASCADE
);
