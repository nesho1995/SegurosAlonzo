#!/bin/bash
ssh -i ~/.ssh/hetzner_seguros_alonzo_ed25519 root@87.99.136.65 "mysql reclamos_auto -e \"
SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE whatsapp_conversaciones;
TRUNCATE TABLE whatsapp_mensajes;
TRUNCATE TABLE whatsapp_envios_log;
TRUNCATE TABLE reclamos_whatsapp;
TRUNCATE TABLE reclamo_documentos;
TRUNCATE TABLE documentos;
TRUNCATE TABLE notificaciones_internas;
TRUNCATE TABLE automatizacion_logs;
TRUNCATE TABLE talleres_detectados;
TRUNCATE TABLE auditoria_logs;
TRUNCATE TABLE correo_revision;
SET FOREIGN_KEY_CHECKS = 1;
SELECT 'whatsapp_conversaciones' as tabla, COUNT(*) as filas FROM whatsapp_conversaciones
UNION ALL SELECT 'whatsapp_mensajes', COUNT(*) FROM whatsapp_mensajes
UNION ALL SELECT 'whatsapp_envios_log', COUNT(*) FROM whatsapp_envios_log
UNION ALL SELECT 'reclamos_whatsapp', COUNT(*) FROM reclamos_whatsapp
UNION ALL SELECT 'reclamo_documentos', COUNT(*) FROM reclamo_documentos
UNION ALL SELECT 'documentos', COUNT(*) FROM documentos
UNION ALL SELECT 'notificaciones_internas', COUNT(*) FROM notificaciones_internas
UNION ALL SELECT 'automatizacion_logs', COUNT(*) FROM automatizacion_logs
UNION ALL SELECT 'talleres_detectados', COUNT(*) FROM talleres_detectados
UNION ALL SELECT 'auditoria_logs', COUNT(*) FROM auditoria_logs
UNION ALL SELECT 'correo_revision', COUNT(*) FROM correo_revision;
\""
