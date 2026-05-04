CREATE TABLE IF NOT EXISTS notificaciones_internas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    usuario_id INT NULL,
    tipo VARCHAR(60) NOT NULL,
    titulo VARCHAR(180) NOT NULL,
    mensaje TEXT NOT NULL,
    entidad_tipo VARCHAR(60) NULL,
    entidad_id INT NULL,
    referencia VARCHAR(160) NOT NULL,
    leida BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_lectura DATETIME NULL,
    UNIQUE KEY uq_notificaciones_referencia (referencia),
    INDEX ix_notificaciones_leida_fecha (leida, fecha_creacion),
    INDEX ix_notificaciones_usuario (usuario_id)
);
