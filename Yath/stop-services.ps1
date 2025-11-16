# Stop all Yath microservices
# Usage: .\stop-services.ps1 [--clean]

param(
    [switch]$Clean
)

Write-Host "🛑 Stopping Yath Microservices" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host ""

if ($Clean) {
    Write-Host "Stopping services and removing volumes..." -ForegroundColor Yellow
    docker-compose down -v
    Write-Host ""
    Write-Host "✓ Services stopped and data volumes removed" -ForegroundColor Green
} else {
    Write-Host "Stopping services (preserving data)..." -ForegroundColor Yellow
    docker-compose down
    Write-Host ""
    Write-Host "✓ Services stopped (data preserved)" -ForegroundColor Green
    Write-Host ""
    Write-Host "To remove all data volumes, run: .\stop-services.ps1 -Clean" -ForegroundColor Yellow
}

Write-Host ""
