# Test Report - 2026-04-29

## Build checks

- Backend: `dotnet build -p:UseAppHost=false -o .\obj\status-build-check`
  - Result: passed, 0 warnings, 0 errors.
  - Note: normal `dotnet build` cannot overwrite `bin\Debug\net8.0` while the local backend process is running.
- Frontend: `npm run build` from `ClientApp`
  - Result: passed.
- Frontend lint: `npm run lint` from `ClientApp`
  - Result: passed.

## Runtime checks

- Backend active: `http://localhost:5000`
- MariaDB active: `127.0.0.1:3307`
- Login smoke test: `POST /api/auth/login` with local admin test credentials passed.

## API smoke tests

Read-only module checks passed:

- `GET /api/auth/me`
- `GET /api/dashboard`
- `GET /api/cartera/clientes?buscar=&estado=TODOS`
- `GET /api/pagos?estado=TODOS`
- `GET /api/recordatorios`
- `GET /api/gastos`
- `GET /api/reclamos?estado=TODOS`
- `GET /api/talleres`
- `GET /api/catalogos`
- `GET /api/auditoria`
- `GET /api/usuarios`
- `GET /api/automatizaciones`
- `GET /api/notificaciones`
- `GET /api/configuracion/empresa`
- `GET /api/configuracion/envios`
- `GET /api/reclamos-config/correo`
- `GET /api/reclamos-config/worker-estado`
- `GET /api/reclamos-config/patrones`
- `GET /api/reclamos-config/plantillas`

## Security publishing note

`appsettings.json` contains local secrets and is intentionally excluded from Git. Publish `appsettings.example.json` only, then configure production values through environment variables or a secret manager.
