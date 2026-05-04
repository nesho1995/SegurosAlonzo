@echo off
setlocal

set "MARIADB_DIR=%USERPROFILE%\tools\mariadb-11.4.7-winx64"
set "DATA_DIR=%USERPROFILE%\tools\mariadb-data-reclamos"
set "SERVER=%MARIADB_DIR%\bin\mariadbd.exe"
set "CONFIG=%DATA_DIR%\my.ini"
set "OUT_LOG=%DATA_DIR%\codex-mariadb.out.log"
set "ERR_LOG=%DATA_DIR%\codex-mariadb.err.log"

if not exist "%SERVER%" (
  echo No se encontro MariaDB en: %SERVER%
  exit /b 1
)

if not exist "%CONFIG%" (
  echo No se encontro la configuracion de datos en: %CONFIG%
  exit /b 1
)

netstat -ano | findstr /R /C:":3307 .*LISTENING" >nul
if %ERRORLEVEL% EQU 0 (
  echo MariaDB ya esta escuchando en el puerto 3307. No se inicia otra instancia.
  exit /b 0
)

echo Iniciando MariaDB con datos persistentes:
echo %DATA_DIR%
start "MariaDB Reclamos" /min "%SERVER%" --defaults-file="%CONFIG%" --console > "%OUT_LOG%" 2> "%ERR_LOG%"
timeout /t 4 /nobreak >nul

netstat -ano | findstr /R /C:":3307 .*LISTENING" >nul
if %ERRORLEVEL% NEQ 0 (
  echo MariaDB no quedo escuchando en 3307.
  echo Revisa logs:
  echo %OUT_LOG%
  echo %ERR_LOG%
  exit /b 1
)

echo MariaDB listo en 127.0.0.1:3307 usando reclamos_auto.
exit /b 0
