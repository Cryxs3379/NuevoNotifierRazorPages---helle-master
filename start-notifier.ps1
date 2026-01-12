# Script de inicio rápido para Notifier con Razor Pages
# Ejecuta ambas APIs necesarias para el funcionamiento completo

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NOTIFIER - Inicio Rápido" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "Notifier-API")) {
    Write-Host "Error: No se encuentra la carpeta Notifier-API" -ForegroundColor Red
    Write-Host "Asegúrate de ejecutar este script desde el directorio raíz del proyecto" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/2] Iniciando API de Llamadas Perdidas (Puerto 5000)..." -ForegroundColor Yellow

# Iniciar API de Llamadas en segundo plano
$callsJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    cd Notifier-APiCalls
    dotnet run
}

Write-Host "  ✓ API de Llamadas iniciada (Job ID: $($callsJob.Id))" -ForegroundColor Green
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "[2/2] Iniciando Notifier con Razor Pages (Puerto 5080)..." -ForegroundColor Yellow

# Esperar un poco más para que la API de llamadas esté lista
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  NOTIFIER INICIADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "URLs disponibles:" -ForegroundColor Cyan
Write-Host "  • Aplicación Web:       http://localhost:5080" -ForegroundColor White
Write-Host "  • Swagger API:          http://localhost:5080/swagger" -ForegroundColor White
Write-Host "  • API Llamadas:         http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "Páginas disponibles:" -ForegroundColor Cyan
Write-Host "  • Inicio:               http://localhost:5080/" -ForegroundColor White
Write-Host "  • Mensajes:             http://localhost:5080/Messages/Index" -ForegroundColor White
Write-Host "  • Enviar Mensaje:       http://localhost:5080/Messages/Reply" -ForegroundColor White
Write-Host "  • Llamadas Perdidas:    http://localhost:5080/Calls/Index" -ForegroundColor White
Write-Host ""
Write-Host "Presiona Ctrl+C para detener ambos servicios" -ForegroundColor Yellow
Write-Host ""

# Iniciar la aplicación principal (en primer plano)
cd Notifier-API
dotnet run

# Limpiar jobs cuando se detenga
Write-Host ""
Write-Host "Deteniendo servicios..." -ForegroundColor Yellow
Stop-Job $callsJob
Remove-Job $callsJob
Write-Host "  ✓ Servicios detenidos" -ForegroundColor Green

