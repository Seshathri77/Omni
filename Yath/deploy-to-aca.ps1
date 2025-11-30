#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys Yath microservices to Azure Container Apps
.DESCRIPTION
    Creates Container Apps Environment and deploys all services with RabbitMQ and Seq
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "yath-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$EnvironmentName = "yath-env",
    
    [Parameter(Mandatory=$false)]
    [string]$AcrName = "yathregistry"
)

Write-Host "Deploying Yath to Azure Container Apps" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

# Check Azure login
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) { throw "Not logged in" }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure!" -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  Environment: $EnvironmentName" -ForegroundColor White
Write-Host ""

# Get ACR credentials
$acrInfo = az acr show --name $AcrName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
if ($null -eq $acrInfo) {
    Write-Host "ACR '$AcrName' not found!" -ForegroundColor Red
    exit 1
}

$acrLoginServer = $acrInfo.loginServer
$acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup --output json | ConvertFrom-Json
$acrUsername = $acrCreds.username
$acrPassword = $acrCreds.passwords[0].value

# Check if Container Apps environment exists
Write-Host "Checking Container Apps environment..." -ForegroundColor Yellow
$envExists = az containerapp env show --name $EnvironmentName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json

if ($null -eq $envExists) {
    Write-Host "Creating Container Apps environment (this may take 5 minutes)..." -ForegroundColor Yellow
    
    az containerapp env create `
        --name $EnvironmentName `
        --resource-group $ResourceGroup `
        --location $Location `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to create environment!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Environment created successfully" -ForegroundColor Green
} else {
    Write-Host "Environment already exists" -ForegroundColor Green
}

Write-Host ""
Write-Host "Deploying infrastructure services..." -ForegroundColor Yellow
Write-Host ""

# Deploy RabbitMQ
Write-Host "  Deploying RabbitMQ..." -ForegroundColor Gray
az containerapp create `
    --name rabbitmq `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image rabbitmq:3.12-management `
    --target-port 5672 `
    --ingress internal `
    --min-replicas 1 `
    --max-replicas 1 `
    --cpu 0.5 `
    --memory 1.0Gi `
    --env-vars `
        RABBITMQ_DEFAULT_USER=guest `
        RABBITMQ_DEFAULT_PASS=guest `
    --output none 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "    RabbitMQ deployed" -ForegroundColor Green
} else {
    Write-Host "    RabbitMQ deployment failed" -ForegroundColor Red
}

# Deploy Seq
Write-Host "  Deploying Seq..." -ForegroundColor Gray
az containerapp create `
    --name seq `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image datalust/seq:latest `
    --target-port 80 `
    --ingress internal `
    --min-replicas 1 `
    --max-replicas 1 `
    --cpu 0.5 `
    --memory 1.0Gi `
    --env-vars `
        ACCEPT_EULA=Y `
    --output none 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "    Seq deployed" -ForegroundColor Green
} else {
    Write-Host "    Seq deployment failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "Deploying microservices..." -ForegroundColor Yellow
Write-Host ""

# Define services
$services = @(
    @{ Name = "user-service"; Image = "yath-user-service" },
    @{ Name = "trip-service"; Image = "yath-trip-service" },
    @{ Name = "activity-service"; Image = "yath-activity-service" },
    @{ Name = "expense-service"; Image = "yath-expense-service" },
    @{ Name = "media-service"; Image = "yath-media-service" },
    @{ Name = "chat-service"; Image = "yath-chat-service" },
    @{ Name = "location-service"; Image = "yath-location-service" },
    @{ Name = "notification-service"; Image = "yath-notification-service" }
)

$deployed = @()
$failed = @()

foreach ($svc in $services) {
    $serviceName = $svc.Name
    $imageName = $svc.Image
    
    Write-Host "  Deploying $serviceName..." -ForegroundColor Gray
    
    az containerapp create `
        --name $serviceName `
        --resource-group $ResourceGroup `
        --environment $EnvironmentName `
        --image "$acrLoginServer/$imageName`:latest" `
        --registry-server $acrLoginServer `
        --registry-username $acrUsername `
        --registry-password $acrPassword `
        --target-port 80 `
        --ingress external `
        --min-replicas 1 `
        --max-replicas 3 `
        --cpu 0.5 `
        --memory 1.0Gi `
        --env-vars `
            ASPNETCORE_ENVIRONMENT=Production `
            ASPNETCORE_URLS=http://+:80 `
        --output none 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    $serviceName deployed" -ForegroundColor Green
        $deployed += $serviceName
    } else {
        Write-Host "    $serviceName deployment failed" -ForegroundColor Red
        $failed += $serviceName
    }
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host "Deployment Summary" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host ""

Write-Host "Services deployed: $($deployed.Count)" -ForegroundColor Green
Write-Host "Services failed: $($failed.Count)" -ForegroundColor $(if ($failed.Count -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($deployed.Count -gt 0) {
    Write-Host "Getting service URLs..." -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($serviceName in $deployed) {
        $app = az containerapp show --name $serviceName --resource-group $ResourceGroup --output json | ConvertFrom-Json
        $fqdn = $app.properties.configuration.ingress.fqdn
        
        Write-Host "  $serviceName`:" -ForegroundColor Cyan
        Write-Host "    URL: https://$fqdn" -ForegroundColor White
        Write-Host "    Swagger: https://$fqdn/swagger" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Management Commands:" -ForegroundColor Cyan
Write-Host "  List apps:       az containerapp list -g $ResourceGroup -o table" -ForegroundColor Gray
Write-Host "  View logs:       az containerapp logs show -n <app-name> -g $ResourceGroup --follow" -ForegroundColor Gray
Write-Host "  Scale app:       az containerapp update -n <app-name> -g $ResourceGroup --min-replicas 2 --max-replicas 5" -ForegroundColor Gray
Write-Host "  Delete all:      az containerapp env delete -n $EnvironmentName -g $ResourceGroup --yes" -ForegroundColor Gray
Write-Host ""

Write-Host "Cost Estimate: ~`$50-100/month with auto-scaling (pay per use)" -ForegroundColor Yellow
Write-Host "Note: RabbitMQ and Seq are accessible via internal DNS (rabbitmq:5672, seq:5341)" -ForegroundColor Yellow
Write-Host ""
