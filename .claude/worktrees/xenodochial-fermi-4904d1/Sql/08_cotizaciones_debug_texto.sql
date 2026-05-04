-- OBSOLETO desde 2026-04-28:
-- Columnas auxiliares del comparador deshabilitado. Se conservan para que
-- instalaciones existentes no fallen por diferencias de esquema historico.

ALTER TABLE cotizacion_archivos
    ADD COLUMN IF NOT EXISTS raw_text LONGTEXT NULL,
    ADD COLUMN IF NOT EXISTS normalized_text LONGTEXT NULL,
    ADD COLUMN IF NOT EXISTS extraction_debug_json LONGTEXT NULL;
