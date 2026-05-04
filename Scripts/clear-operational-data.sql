-- Limpieza operativa para recargar cartera desde cero.
-- Conserva usuarios, roles, permisos, catalogos y configuracion.

DROP PROCEDURE IF EXISTS truncate_if_exists;

DELIMITER //
CREATE PROCEDURE truncate_if_exists(IN table_name_value VARCHAR(128))
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = DATABASE()
          AND table_name = table_name_value
    ) THEN
        SET @truncate_sql = CONCAT('TRUNCATE TABLE `', REPLACE(table_name_value, '`', '``'), '`');
        PREPARE stmt FROM @truncate_sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END//
DELIMITER ;

SET FOREIGN_KEY_CHECKS = 0;

CALL truncate_if_exists('recordatorios');
CALL truncate_if_exists('poliza_pagos');
CALL truncate_if_exists('poliza_cuotas');
CALL truncate_if_exists('documentos');
CALL truncate_if_exists('reclamo_documentos');
CALL truncate_if_exists('reclamos_whatsapp');
CALL truncate_if_exists('whatsapp_envios_log');
CALL truncate_if_exists('notificaciones_internas');
CALL truncate_if_exists('automatizacion_logs');
CALL truncate_if_exists('vehiculo_cambios');
CALL truncate_if_exists('vehiculo_historial');
CALL truncate_if_exists('vehiculos');
CALL truncate_if_exists('cliente_telefonos');
CALL truncate_if_exists('polizas');
CALL truncate_if_exists('clientes');
CALL truncate_if_exists('carteraclientes');

SET FOREIGN_KEY_CHECKS = 1;

DROP PROCEDURE IF EXISTS truncate_if_exists;
