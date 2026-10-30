@echo off
setlocal
cd /d "%~dp0"
where powershell.exe >nul 2>nul
if errorlevel 1 (
    echo [ERRO] Windows PowerShell nao encontrado.
    pause
    exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0compilar_spartanport.ps1"
set "RESULTADO=%ERRORLEVEL%"
echo.
if not "%RESULTADO%"=="0" (
    echo [FALHA] Veja o caminho do log mostrado acima.
) else (
    echo [OK] Compilacao finalizada. Localize a pasta artefatos.
)
pause
exit /b %RESULTADO%
