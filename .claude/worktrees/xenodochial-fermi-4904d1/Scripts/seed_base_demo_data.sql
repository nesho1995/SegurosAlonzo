USE reclamos_auto;

-- Seguridad base (roles + columnas de login)
SOURCE C:/Users/nhern/OneDrive/Documents/ReclamosWhatsApp/Sql/06_roles_seguridad.sql;
INSERT IGNORE INTO roles (Name) VALUES ('ADMIN');

-- Configuracion base de plataforma
SOURCE C:/Users/nhern/OneDrive/Documents/ReclamosWhatsApp/Sql/12_plataforma_correduria.sql;

-- Catalogos e indices base
SOURCE C:/Users/nhern/OneDrive/Documents/ReclamosWhatsApp/Sql/15_catalogos_admin.sql;

-- Tablas de talleres + settings extractor
SOURCE C:/Users/nhern/OneDrive/Documents/ReclamosWhatsApp/Sql/04_extractor_talleres.sql;

-- Catalogos minimos de operacion
INSERT INTO catalogos (tipo_catalogo, codigo, nombre, descripcion, activo, orden, es_default, pendiente_revision)
VALUES
('RAMOS', 'AUTO', 'AUTO', 'Ramo autos', 1, 1, 1, 0),
('RAMOS', 'MEDICO', 'MEDICO', 'Ramo medico', 1, 2, 0, 0),
('ASEGURADORAS', 'SEGUROS_ALONZO', 'SEGUROS ALONZO', 'Aseguradora principal', 1, 1, 1, 0),
('ASEGURADORAS', 'MAPFRE', 'MAPFRE', 'Aseguradora', 1, 2, 0, 0),
('FORMAS_PAGO', 'CONTADO', 'CONTADO', 'Pago de contado', 1, 1, 1, 0),
('FORMAS_PAGO', 'MENSUAL', 'MENSUAL', 'Pago mensual', 1, 2, 0, 0),
('MEDIOS', 'WHATSAPP', 'WHATSAPP', 'Canal WhatsApp', 1, 1, 1, 0),
('MEDIOS', 'EMAIL', 'EMAIL', 'Canal correo', 1, 2, 0, 0),
('ESTADOS_PAGO', 'SIN_VALIDAR', 'SIN_VALIDAR', 'Estado inicial', 1, 1, 1, 0),
('ESTADOS_PAGO', 'EN_CUOTAS', 'EN_CUOTAS', 'Con cuotas pendientes', 1, 2, 0, 0),
('ESTADOS_PAGO', 'PAGADO', 'PAGADO', 'Pago completado', 1, 3, 0, 0),
('EMISION_RENOVACION', 'EMISION', 'EMISION', 'Poliza emitida', 1, 1, 1, 0),
('EMISION_RENOVACION', 'RENOVACION', 'RENOVACION', 'Poliza renovada', 1, 2, 0, 0)
ON DUPLICATE KEY UPDATE
    nombre = VALUES(nombre),
    descripcion = VALUES(descripcion),
    activo = VALUES(activo),
    pendiente_revision = VALUES(pendiente_revision);

-- Talleres demo
INSERT INTO talleres (nombre, ciudad, aseguradora, ramo, telefono, direccion, activo)
VALUES
('Taller Centro Express', 'Tegucigalpa', 'SEGUROS ALONZO', 'AUTO', '50499991111', 'Col. Centro, Tegucigalpa', 1),
('Taller Norte Premium', 'San Pedro Sula', 'MAPFRE', 'AUTO', '50499992222', 'Boulevard del Norte, SPS', 1),
('Clinica Medica Integral', 'Tegucigalpa', 'SEGUROS ALONZO', 'MEDICO', '50499993333', 'Col. Palmira, Tegucigalpa', 1);
