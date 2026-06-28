@echo off
setlocal

REM Ejecuta el analizador desde la carpeta donde esté este .bat.
cd /d "%~dp0"

python .\analyze_telemetry.py "%USERPROFILE%\AppData\LocalLow\AppleAxion\HeartBeat" --json-out ".\resumen_telemetria.json" --pretty

if errorlevel 1 (
    echo.
    echo [ERROR] El analisis no se ha podido completar.
    pause
    exit /b %errorlevel%
)

echo.
echo [OK] Analisis completado. Se ha generado resumen_telemetria.json
pause
endlocal
