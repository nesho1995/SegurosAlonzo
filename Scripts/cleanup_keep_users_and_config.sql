-- Limpieza operativa: conserva usuarios y tablas de configuracion.
-- Base objetivo: reclamos_auto

SET FOREIGN_KEY_CHECKS = 0;

-- Operacion de cartera/polizas/clientes
TRUNCATE TABLE cliente_telefonos;
TRUNCATE TABLE poliza_pagos;
TRUNCATE TABLE poliza_cuotas;
TRUNCATE TABLE polizas;
TRUNCATE TABLE vehiculo_historial;
TRUNCATE TABLE vehiculos;
TRUNCATE TABLE clientes;

-- Operacion de reclamos/documentos
TRUNCATE TABLE reclamo_documentos;
TRUNCATE TABLE reclamos_whatsapp;
TRUNCATE TABLE documentos;

-- Operacion de notificaciones/comercial/finanzas
TRUNCATE TABLE recordatorios;
TRUNCATE TABLE whatsapp_envios_log;
TRUNCATE TABLE notificaciones_internas;
TRUNCATE TABLE comisiones_detalle;
TRUNCATE TABLE comisiones_lotes;
TRUNCATE TABLE gastos;
TRUNCATE TABLE carteraclientes;

-- Operacion de talleres
TRUNCATE TABLE talleres_detectados;
TRUNCATE TABLE talleres;

-- Operacion de automatizacion (logs)
TRUNCATE TABLE automatizacion_logs;

SET FOREIGN_KEY_CHECKS = 1;
