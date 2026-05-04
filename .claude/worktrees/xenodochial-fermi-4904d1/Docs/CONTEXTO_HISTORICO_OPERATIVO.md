# Contexto Historico Operativo - ReclamosWhatsApp

## Objetivo de este documento

Concentrar las decisiones tecnicas y operativas tomadas durante las iteraciones recientes para reducir retrabajo y acelerar cambios futuros.

## Estado funcional actual

- Backend: ASP.NET Core .NET 8 con Dapper/MariaDB.
- Frontend: React SPA.
- Motor de base de datos activo en `127.0.0.1:3307`.
- Base activa esperada: `reclamos_auto` (datadir correcto en `C:/Users/nhern/tools/mariadb-data-reclamos`).
- Backend expuesto en `http://localhost:5000`.

## Importador de cartera: reglas clave vigentes

- Soporte de cuotas ampliado a **12 cuotas maximo**:
  - Columnas reconocidas `CUOTA 1 ... CUOTA 12`.
  - `CUOTAS` se acota internamente a 12.
  - Si `CUOTAS` viene vacio, se infiere por columnas de cuota con monto.
  - Si no hay cuotas y `FORMA PAGO` es `CONTADO`/`EXTRA`, se asume 1.
- Plantilla financiera actualizada para incluir `CUOTA 11` y `CUOTA 12`.
- Deteccion de duplicados ajustada para polizas colectivas:
  - Duplicado exacto por combinacion `cliente + poliza + item + certificado + endoso`.
  - Misma poliza con cliente distinto se permite (caso colectivo real).
- Filas sin poliza:
  - Se procesa/actualiza cliente.
  - Se omite insercion de poliza en esa fila.

## Mapeos y aliases importantes de Excel

- Nombre cliente (`NOMBRE`) acepta variantes como:
  - `CLIENTE`, `CLIENTE2`, `CLIENTE_2`, `ASEGURADO`, `NOMBREASEGURADO`.
- Financiera/contratante (`CLIENTE FINANCIERA`) acepta:
  - `FINANCIERA`, `CONTRATANTE`, `CLIENTECONTRATANTE`.
- Poliza (`POLIZA`) acepta:
  - `NUMEROPOLIZA`, `NOPOLIZA`, `POLIZANUMERO`, `NUMPOLIZA`, `PÓLIZA`.
- Observaciones/agente:
  - `AGENTE/OBSERVACIONES` se contempla para no perder informacion en archivos historicos.

## Ajustes de datos y consistencia

- Se agregaron/aseguraron columnas de soporte en esquema (incluye `fecha_actualizacion`).
- Se corrigio error de SQL ambiguo en consultas de polizas al incluir join de contratante:
  - Todas las columnas de `polizas` deben ir prefijadas con alias `p.` cuando exista join con `clientes`.

## UX y filtros en modulo Clientes

- Filtros extendidos (backend y frontend):
  - `buscar`, `estado`, `financiera`, `aseguradora`, `ramo`, `estadoPago`, `ciudad`.
- Vista de polizas mas comoda para ejecutivos:
  - Modo compacto con boton de expandir/contraer detalle por poliza.
  - Menor scroll inicial y mejor enfoque operativo.
- Campo financiera en detalle de poliza:
  - Mostrar nombre real del contratante (`ClienteContratanteNombre`) y no solo ID.

## Limpieza para pruebas

- Script recomendado para dejar datos operativos en limpio sin borrar usuarios/config:
  - `Scripts/cleanup_keep_users_and_config.sql`.
- Verificacion posterior recomendada:
  - `clientes`, `polizas`, `vehiculos`, `poliza_cuotas`, `documentos` en cero.

## Consideraciones operativas

- Si frontend muestra "base no disponible", validar primero:
  1. `mariadbd` escuchando en `3307`.
  2. backend en `5000`.
  3. que no haya error SQL runtime en log de backend.
- El datadir correcto para esta instalacion es el de `mariadb-data-reclamos`; no levantar con datadir vacio/alterno.

## Proximos pasos recomendados

- Autocomplete de financiera en filtros de clientes.
- Hardening de consultas con joins para evitar ambiguedades futuras.
- Suite de smoke test para importador (cartera normal y financiera).
- Commit granular por area: importador, consultas, UI/filtros, operaciones DB.
