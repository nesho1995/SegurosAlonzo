# Deploy catalogos y contexto (2026-04-29)

## Objetivo
- Mantener un seed reproducible de catalogos operativos para deploy.
- Guardar el contexto funcional acordado para importacion y gestion de polizas/cuotas.

## Script de catalogos para deploy
- Archivo: `Sql/16_catalogos_operativos_seed.sql`
- Incluye:
  - `COMPANIAS`
  - `RAMOS`
  - `FORMAS_PAGO`
  - `EMISION_RENOVACION`
  - `MEDIOS`
  - `ENDOSATARIOS`
- Es idempotente (`ON DUPLICATE KEY UPDATE`).
- Normaliza nombres visibles para UI sin `_` en etiqueta.

## Contexto funcional acordado
- Cuotas:
  - Si no vienen montos por cuota en Excel, se inicializan en `0.00`.
  - El ejecutivo puede ajustar manualmente el monto por cuota desde UI.
  - Al crear poliza manual con `cuotas = N`, se crean N cuotas iniciales en `0.00`.
  - La grilla de cuotas muestra la cantidad definida en la poliza (no siempre 12).
- Financiera/contratante:
  - En edicion de poliza se captura nombre (no ID numerico).
  - Backend resuelve/crea el contratante y guarda `cliente_contratante_id`.
  - Evita mostrar valores como `Cliente #2` cuando ya existe nombre.
- Numero item:
  - Campo opcional.
  - Si llega vacio, se guarda como `NULL` para evitar error SQL.
  - Se removio del formulario de poliza por solicitud operativa.

## Notas para despliegue
- Ejecutar este orden:
  1) schema base (`01_create_database.sql` + `03_update_schema.sql`)
  2) catalogos base (`15_catalogos_admin.sql`)
  3) catalogos operativos (`16_catalogos_operativos_seed.sql`)
- Validacion sugerida:
  - `SELECT tipo_catalogo, COUNT(*) FROM catalogos GROUP BY tipo_catalogo;`
  - revisar en UI que los nombres visibles no tengan `_`.

## Operacion local aplicada en esta sesion
- Limpieza de datos de prueba de cartera:
  - `clientes`
  - `polizas`
  - `poliza_cuotas`
  - `poliza_pagos`
  - `cliente_telefonos`
  - `recordatorios`
