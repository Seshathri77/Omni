#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pushes Yath microservices Docker images to Azure Container Registry
.DESCRIPTION
    This script tags and pushes all Yath Docker images to Azure Container Registry (ACR)
.PARAMETER ResourceGroup
    Azure Resource Group name
.PARAMETER AcrName
    Azure Container Registry name
.EXAMPLE
    .\deploy-to-acr.ps1 -ResourceGroup "yath-rg" -AcrName "yathregistry"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "yath-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$AcrName = "yathregistry"
)

Write-Host "Deploying Yath Microservices to Azure Container Registry" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "Azure CLI is installed (version $($azVersion.'azure-cli'))" -ForegroundColor Green
} catch {
    Write-Host "Azure CLI is not installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Azure CLI from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

# Check if logged in to Azure
Write-Host ""
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) {
        throw "Not logged in"
    }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "Subscription: $($account.name) ($($account.id))" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Running 'az login'..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Azure login failed!" -ForegroundColor Red
        exit 1
    }
}

# Check if Docker is running
Write-Host ""
Write-Host "Checking Docker status..." -ForegroundColor Yellow
try {
    docker ps > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker not running"
    }
    Write-Host "Docker is running" -ForegroundColor Green
} catch {
    Write-Host "Docker is not running!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start Docker Desktop and run this script again." -ForegroundColor Yellow
    exit 1
}

# Check if images exist
Write-Host ""
Write-Host "Checking for Docker images..." -ForegroundColor Yellow
$services = @(
    "user-service",
    "trip-service",
    "activity-service",
    "expense-service",
    "media-service",
    "chat-service",
    "location-service",
    "notification-service"
)

$missingImages = @()
foreach ($service in $services) {
    $image = "yath-${service}:latest"
    $exists = docker images -q $image
    if ([string]::IsNullOrEmpty($exists)) {
        $missingImages += $image
    } else {
        Write-Host "  Found: $image" -ForegroundColor Green
    }
}

if ($missingImages.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing Docker images:" -ForegroundColor Red
    foreach ($img in $missingImages) {
        Write-Host "  - $img" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Please build images first by running: .\build-images.ps1" -ForegroundColor Yellow
    exit 1
}

# Check if resource group exists
Write-Host ""
Write-Host "Checking Azure Resource Group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    Write-Host "Resource group '$ResourceGroup' does not exist!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please create it first by running: .\setup-azure-infrastructure.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host "Resource group '$ResourceGroup' exists" -ForegroundColor Green

# Check if ACR exists
Write-Host ""
Write-Host "Checking Azure Container Registry..." -ForegroundColor Yellow
try {
    $acrInfo = az acr show --name $AcrName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($null -eq $acrInfo) {
        throw "ACR not found"
    }
    Write-Host "ACR '$AcrName' exists" -ForegroundColor Green
    Write-Host "  Login Server: $($acrInfo.loginServer)" -ForegroundColor Cyan
} catch {
    Write-Host "ACR '$AcrName' does not exist in resource group '$ResourceGroup'!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please create it first by running: .\setup-azure-infrastructure.ps1" -ForegroundColor Yellow
    exit 1
}

$acrLoginServer = $acrInfo.loginServer

# Login to ACR
Write-Host ""
Write-Host "Logging in to Azure Container Registry..." -ForegroundColor Yellow
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to login to ACR!" -ForegroundColor Red
    exit 1
}
Write-Host "Logged in to ACR" -ForegroundColor Green

# Tag and push images
Write-Host ""
Write-Host "Tagging and pushing images to ACR..." -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date
$successCount = 0
$failCount = 0

foreach ($service in $services) {
    $localImage = "yath-${service}:latest"
    $remoteImage = "$acrLoginServer/yath-${service}:latest"
    
    Write-Host "Processing: $service" -ForegroundColor Cyan
    Write-Host "  Tagging: $localImage -> $remoteImage" -ForegroundColor Gray
    
    docker tag $localImage $remoteImage
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed to tag image" -ForegroundColor Red
        $failCount++
        continue
    }
    
    Write-Host "  Pushing to ACR..." -ForegroundColor Gray
    docker push $remoteImage
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed to push image" -ForegroundColor Red
        $failCount++
        continue
    }
    
    Write-Host "  Successfully pushed" -ForegroundColor Green
    $successCount++
    Write-Host ""
}

$endTime = Get-Date
$duration = $endTime - $startTime

# Summary
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Green
if ($failCount -eq 0) {
    Write-Host "All images pushed successfully!" -ForegroundColor Green
} else {
    Write-Host "Push completed with errors" -ForegroundColor Yellow
}
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total services: $($services.Count)" -ForegroundColor White
Write-Host "  Successful: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "  Duration: $($duration.Minutes)m $($duration.Seconds)s" -ForegroundColor White
Write-Host ""
Write-Host "ACR Details:" -ForegroundColor Cyan
Write-Host "  Registry: $acrLoginServer" -ForegroundColor White
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host ""

if ($successCount -gt 0) {
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Deploy to Azure Container Instances: .\deploy-to-aci.ps1" -ForegroundColor White
    Write-Host "  2. View images in portal: https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroup/providers/Microsoft.ContainerRegistry/registries/$AcrName/repository" -ForegroundColor White
    Write-Host ""
}

if ($failCount -eq 0) {
    exit 0
} else {
    exit 1
}
