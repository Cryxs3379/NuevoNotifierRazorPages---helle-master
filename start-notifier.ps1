# Script de inicio rapido para Notifier con Razor Pages
# Ejecuta ambas APIs necesarias para el funcionamiento completo
# .\start-notifier.ps1

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NOTIFIER - Inicio Rapido" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "Notifier-API")) {
    Write-Host "Error: No se encuentra la carpeta Notifier-API" -ForegroundColor Red
    Write-Host "Asegurate de ejecutar este script desde el directorio raiz del proyecto" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path "Notifier-APiCalls")) {
    Write-Host "Error: No se encuentra la carpeta Notifier-APiCalls" -ForegroundColor Red
    Write-Host "Asegurate de ejecutar este script desde el directorio raiz del proyecto" -ForegroundColor Yellow
    exit 1
}

$rootPath = (Get-Location).Path
$callsProcess = $null

# Funcion para limpiar procesos al salir
function Cleanup {
    if ($null -ne $callsProcess -and -not $callsProcess.HasExited) {
        Write-Host ""
        Write-Host "Deteniendo API de Llamadas..." -ForegroundColor Yellow
        try {
            Stop-Process -Id $callsProcess.Id -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] API de Llamadas detenida" -ForegroundColor Green
        }
        catch {
            Write-Host "  [WARN] No se pudo detener la API de Llamadas (puede que ya este cerrada)" -ForegroundColor Yellow
        }
    }
}

Write-Host "[1/2] Iniciando API de Llamadas Perdidas..." -ForegroundColor Yellow

# Iniciar API de Llamadas en ventana separada
try {
    $callsProcess = Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-Command",
        "cd '$rootPath\Notifier-APiCalls'; Write-Host 'API de Llamadas Perdidas - Puerto 5001' -ForegroundColor Cyan; Write-Host 'Presiona Ctrl+C para detener' -ForegroundColor Yellow; Write-Host ''; dotnet run"
    ) -PassThru

    if ($null -ne $callsProcess) {
        Write-Host "  [OK] API de Llamadas iniciada (PID: $($callsProcess.Id))" -ForegroundColor Green
        Write-Host "  Ventana separada abierta para ver logs" -ForegroundColor Gray
        Start-Sleep -Seconds 3
    }
    else {
        Write-Host "  [WARN] No se pudo iniciar la API de Llamadas" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  [ERROR] Error al iniciar API de Llamadas: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/2] Iniciando Notifier con Razor Pages..." -ForegroundColor Yellow

# Esperar un poco mas para que la API de llamadas este lista
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  NOTIFIER INICIADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "URLs disponibles:" -ForegroundColor Cyan
Write-Host "  * Aplicacion Web:       http://localhost:5080" -ForegroundColor White
Write-Host "  * Swagger API:          http://localhost:5080/swagger" -ForegroundColor White
Write-Host "  * API Llamadas:         http://localhost:5001" -ForegroundColor White
Write-Host ""
Write-Host "Paginas disponibles:" -ForegroundColor Cyan
Write-Host "  * Inicio:               http://localhost:5080/" -ForegroundColor White
Write-Host "  * Mensajes:             http://localhost:5080/Messages/Index" -ForegroundColor White
Write-Host "  * Enviar Mensaje:       http://localhost:5080/Messages/Reply" -ForegroundColor White
Write-Host "  * Llamadas Perdidas:    http://localhost:5080/Calls/Index" -ForegroundColor White
Write-Host ""
Write-Host "Presiona Ctrl+C para detener ambos servicios" -ForegroundColor Yellow
Write-Host ""

# Iniciar la aplicacion principal (en primer plano)
try {
    Push-Location Notifier-API
    dotnet run
}
catch {
    Write-Host ""
    Write-Host "Error al ejecutar Notifier-API: $_" -ForegroundColor Red
}
finally {
    # Volver al directorio original
    Pop-Location
    # Limpiar al finalizar
    Cleanup
}
