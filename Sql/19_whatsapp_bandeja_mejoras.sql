USE reclamos_auto;

ALTER TABLE whatsapp_conversaciones
    ADD COLUMN IF NOT EXISTS reclamo_id INT NULL AFTER cliente_id;
