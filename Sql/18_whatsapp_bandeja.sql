USE reclamos_auto;

CREATE TABLE IF NOT EXISTS whatsapp_conversaciones (
    id                  INT          AUTO_INCREMENT PRIMARY KEY,
    telefono            VARCHAR(50)  NOT NULL,
    nombre_contacto     VARCHAR(180) NULL,
    cliente_id          INT          NULL,
    estado              VARCHAR(30)  NOT NULL DEFAULT 'abierta',
    ultima_actividad    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    no_leidos           INT          NOT NULL DEFAULT 0,
    agente_asignado_id  INT          NULL,
    creado_en           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_wa_conv_telefono  (telefono),
    INDEX ix_wa_conv_estado         (estado),
    INDEX ix_wa_conv_actividad      (ultima_actividad DESC),
    INDEX ix_wa_conv_cliente        (cliente_id)
);

CREATE TABLE IF NOT EXISTS whatsapp_mensajes (
    id                  INT          AUTO_INCREMENT PRIMARY KEY,
    conversacion_id     INT          NOT NULL,
    whatsapp_message_id VARCHAR(200) NULL,
    direccion           VARCHAR(10)  NOT NULL,
    tipo_contenido      VARCHAR(20)  NOT NULL DEFAULT 'texto',
    contenido           TEXT         NULL,
    media_id            VARCHAR(200) NULL,
    media_url           VARCHAR(500) NULL,
    media_tipo_mime     VARCHAR(100) NULL,
    media_nombre        VARCHAR(200) NULL,
    estado              VARCHAR(20)  NOT NULL DEFAULT 'recibido',
    usuario_id          INT          NULL,
    creado_en           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_wa_msg_conv  (conversacion_id),
    INDEX ix_wa_msg_wa_id (whatsapp_message_id),
    CONSTRAINT fk_wa_msg_conv FOREIGN KEY (conversacion_id)
        REFERENCES whatsapp_conversaciones(id) ON DELETE CASCADE
);
