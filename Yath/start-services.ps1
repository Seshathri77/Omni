# Start all Yath microservices
# Usage: .\start-services.ps1

Write-Host "🚀 Starting Yath Microservices" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
try {
    docker info | Out-Null
    Write-Host "✓ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker is not running!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start Docker Desktop and run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Starting infrastructure and services..." -ForegroundColor Yellow
Write-Host ""

# Start services
docker-compose up -d

Write-Host ""
Write-Host "Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Check status
Write-Host ""
Write-Host "Service Status:" -ForegroundColor Cyan
docker-compose ps

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Green
Write-Host "✓ Services started!" -ForegroundColor Green
Write-Host ""
Write-Host "Access Points:" -ForegroundColor Cyan
Write-Host "  User Service:         http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  Trip Service:         http://localhost:5001/swagger" -ForegroundColor White
Write-Host "  Activity Service:     http://localhost:5002/swagger" -ForegroundColor White
Write-Host "  Expense Service:      http://localhost:5003/swagger" -ForegroundColor White
Write-Host "  Media Service:        http://localhost:5004/swagger" -ForegroundColor White
Write-Host "  Chat Service:         http://localhost:5005/swagger" -ForegroundColor White
Write-Host "  Location Service:     http://localhost:5006/swagger" -ForegroundColor White
Write-Host "  Notification Service: http://localhost:5007/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Infrastructure:" -ForegroundColor Cyan
Write-Host "  MongoDB:              localhost:27017 (admin/admin123)" -ForegroundColor White
Write-Host "  RabbitMQ UI:          http://localhost:15672 (guest/guest)" -ForegroundColor White
Write-Host "  Seq Logs:             http://localhost:5341 (Admin123!)" -ForegroundColor White
Write-Host "  Azurite Blob:         http://localhost:10000" -ForegroundColor White
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Cyan
Write-Host "  View logs:       docker-compose logs -f [service-name]" -ForegroundColor White
Write-Host "  Stop services:   docker-compose down" -ForegroundColor White
Write-Host "  Restart service: docker-compose restart [service-name]" -ForegroundColor White
Write-Host ""
