@echo off
setlocal

set "MYSQL=%USERPROFILE%\tools\mariadb-11.4.7-winx64\bin\mysql.exe"
set "SQL=%~dp0clear-operational-data.sql"

if not exist "%MYSQL%" (
  echo No se encontro mysql.exe en: %MYSQL%
  exit /b 1
)

if not exist "%SQL%" (
  echo No se encontro el script SQL en: %SQL%
  exit /b 1
)

netstat -ano | findstr /R /C:":3307 .*LISTENING" >nul
if %ERRORLEVEL% NEQ 0 (
  echo MariaDB no esta escuchando en 3307.
  echo Primero ejecuta: Scripts\start-mariadb-reclamos.cmd
  exit /b 1
)

echo.
echo ADVERTENCIA: esto borrara datos operativos de clientes, polizas, cuotas, pagos,
echo documentos operativos, reclamos, recordatorios, vehiculos y logs relacionados.
echo Conserva usuarios, roles, permisos, catalogos y configuracion.
echo.
choice /M "Confirmas la limpieza operativa"
if %ERRORLEVEL% NEQ 1 (
  echo Cancelado.
  exit /b 0
)

"%MYSQL%" -h127.0.0.1 -P3307 -uroot -p -D reclamos_auto < "%SQL%"
if %ERRORLEVEL% NEQ 0 (
  echo La limpieza fallo. Revisa el error anterior.
  exit /b 1
)

echo Limpieza operativa completada.
exit /b 0
