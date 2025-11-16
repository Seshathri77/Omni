# Build all Yath microservice Docker images
# Usage: .\build-images.ps1

Write-Host "🚀 Building Yath Microservices Docker Images" -ForegroundColor Cyan
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
    Write-Host "On Windows: Search for 'Docker Desktop' and launch it" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Building all microservices..." -ForegroundColor Yellow
Write-Host ""

# Build all services
$startTime = Get-Date

try {
    docker-compose build --no-cache
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor Green
    Write-Host "✓ All images built successfully!" -ForegroundColor Green
    Write-Host "Build time: $($duration.ToString('mm\:ss'))" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Start all services:  docker-compose up -d" -ForegroundColor White
    Write-Host "  2. View logs:          docker-compose logs -f" -ForegroundColor White
    Write-Host "  3. Check status:       docker-compose ps" -ForegroundColor White
    Write-Host "  4. Stop services:      docker-compose down" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "✗ Build failed!" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
