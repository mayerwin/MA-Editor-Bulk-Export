@echo off
setlocal
echo =================================================================
echo   Bulk Export Tool For Microsoft Advertising Editor
echo =================================================================
echo.

cd /d "%~dp0"

dotnet run --project "%~dp0src\MAEditorBulkExport.csproj" -- %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Process completed with exit code %ERRORLEVEL%.
)
echo.
pause
