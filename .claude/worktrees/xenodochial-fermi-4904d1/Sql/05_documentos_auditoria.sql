CREATE TABLE IF NOT EXISTS documentos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    entidad_tipo VARCHAR(30) NOT NULL,
    entidad_id INT NOT NULL,
    tipo_documento VARCHAR(80) NOT NULL,
    nombre_archivo_original VARCHAR(255) NOT NULL,
    nombre_archivo_guardado VARCHAR(255) NOT NULL,
    ruta_relativa VARCHAR(500) NOT NULL,
    ruta_archivo VARCHAR(500) NULL,
    mime_type VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
    tamano_bytes BIGINT NOT NULL DEFAULT 0,
    hash_archivo VARCHAR(128) NULL,
    subido_por_usuario_id INT NULL,
    fecha_subida DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    extension VARCHAR(10) NOT NULL,
    activo TINYINT(1) NOT NULL DEFAULT 1,
    INDEX ix_documentos_entidad (entidad_tipo, entidad_id),
    INDEX ix_documentos_fecha (fecha_subida)
);

CREATE TABLE IF NOT EXISTS auditoria_logs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    usuario_id INT NULL,
    accion VARCHAR(80) NOT NULL,
    entidad_tipo VARCHAR(30) NOT NULL,
    entidad_id INT NULL,
    descripcion VARCHAR(1000) NOT NULL,
    fecha DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ip VARCHAR(60) NULL,
    INDEX ix_auditoria_fecha (fecha),
    INDEX ix_auditoria_entidad (entidad_tipo, entidad_id),
    INDEX ix_auditoria_usuario (usuario_id)
);
