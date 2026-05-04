USE reclamos_auto;

ALTER TABLE clientes
    ADD COLUMN IF NOT EXISTS telefono_secundario VARCHAR(50) NULL AFTER telefono,
    ADD COLUMN IF NOT EXISTS telefonos_extra_json LONGTEXT NULL AFTER telefono_secundario,
    ADD COLUMN IF NOT EXISTS correos_extra_json LONGTEXT NULL AFTER email;

ALTER TABLE polizas
    ADD COLUMN IF NOT EXISTS mes_inicio_poliza VARCHAR(50) NULL AFTER suma_asegurada;
