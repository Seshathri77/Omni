#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys Yath microservices to Azure Container Instances
.DESCRIPTION
    Creates Azure Container Instances for all Yath microservices with proper networking and configuration
.PARAMETER ResourceGroup
    Azure Resource Group name
.PARAMETER Location
    Azure region
.PARAMETER AcrName
    Azure Container Registry name
.EXAMPLE
    .\deploy-to-aci.ps1 -ResourceGroup "yath-rg" -Location "eastus" -AcrName "yathregistry"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "yath-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$AcrName = "yathregistry",
    
    [Parameter(Mandatory=$false)]
    [string]$MongoConnectionString = "mongodb://admin:admin123@mongodb:27017",
    
    [Parameter(Mandatory=$false)]
    [string]$RabbitMQHost = "rabbitmq",
    
    [Parameter(Mandatory=$false)]
    [string]$SeqUrl = "http://seq:5341"
)

Write-Host "Deploying Yath Microservices to Azure Container Instances" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed and logged in
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) {
        throw "Not logged in"
    }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure!" -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Check if ACR exists
Write-Host ""
Write-Host "Checking Azure Container Registry..." -ForegroundColor Yellow
try {
    $acrInfo = az acr show --name $AcrName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($null -eq $acrInfo) {
        throw "ACR not found"
    }
    Write-Host "ACR '$AcrName' exists" -ForegroundColor Green
    $acrLoginServer = $acrInfo.loginServer
} catch {
    Write-Host "ACR '$AcrName' not found!" -ForegroundColor Red
    Write-Host "Please run: .\setup-azure-infrastructure.ps1" -ForegroundColor Yellow
    exit 1
}

# Get ACR credentials
$acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup --output json | ConvertFrom-Json
$acrUsername = $acrCreds.username
$acrPassword = $acrCreds.passwords[0].value

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  ACR: $acrLoginServer" -ForegroundColor White
Write-Host ""

# Define services with their ports - Reduced CPU to fit 4-core quota
$services = @(
    @{ Name = "user-service"; Port = 5000; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "trip-service"; Port = 5001; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "activity-service"; Port = 5002; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "expense-service"; Port = 5003; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "media-service"; Port = 5004; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "chat-service"; Port = 5005; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "location-service"; Port = 5006; Cpu = 0.5; Memory = 1.0 },
    @{ Name = "notification-service"; Port = 5007; Cpu = 0.5; Memory = 1.0 }
)

Write-Host "Deploying services..." -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date
$deployed = @()
$failed = @()

foreach ($svc in $services) {
    $serviceName = $svc.Name
    $containerName = "yath-${serviceName}"
    $image = "${acrLoginServer}/yath-${serviceName}:latest"
    $port = $svc.Port
    $cpu = $svc.Cpu
    $memory = $svc.Memory
    
    Write-Host "Deploying: $containerName" -ForegroundColor Cyan
    Write-Host "  Image: $image" -ForegroundColor Gray
    Write-Host "  Port: $port" -ForegroundColor Gray
    Write-Host "  Resources: $cpu CPU, $memory GB RAM" -ForegroundColor Gray
    
    # Check if container already exists
    $existing = az container show --name $containerName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    if ($null -ne $existing) {
        Write-Host "  Container already exists, deleting..." -ForegroundColor Yellow
        az container delete --name $containerName --resource-group $ResourceGroup --yes --output none 2>$null
        Start-Sleep -Seconds 5
    }
    
    # Create container instance
    Write-Host "  Creating container instance..." -ForegroundColor Gray
    
    try {
        az container create `
            --name $containerName `
            --resource-group $ResourceGroup `
            --location $Location `
            --image $image `
            --registry-login-server $acrLoginServer `
            --registry-username $acrUsername `
            --registry-password $acrPassword `
            --cpu $cpu `
            --memory $memory `
            --ports 80 `
            --dns-name-label $containerName `
            --environment-variables `
                ASPNETCORE_ENVIRONMENT=Production `
                ASPNETCORE_URLS=http://+:80 `
            --output none
        
        if ($LASTEXITCODE -eq 0) {
            $containerInfo = az container show --name $containerName --resource-group $ResourceGroup --output json | ConvertFrom-Json
            $fqdn = $containerInfo.ipAddress.fqdn
            Write-Host "  Deployed successfully" -ForegroundColor Green
            Write-Host "  URL: http://$fqdn" -ForegroundColor Cyan
            $deployed += @{ Name = $containerName; Fqdn = $fqdn; Port = $port }
        } else {
            Write-Host "  Deployment failed" -ForegroundColor Red
            $failed += $containerName
        }
    } catch {
        Write-Host "  Deployment failed: $_" -ForegroundColor Red
        $failed += $containerName
    }
    
    Write-Host ""
}

$endTime = Get-Date
$duration = $endTime - $startTime

# Summary
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Green
if ($failed.Count -eq 0) {
    Write-Host "All services deployed successfully!" -ForegroundColor Green
} else {
    Write-Host "Deployment completed with errors" -ForegroundColor Yellow
}
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total services: $($services.Count)" -ForegroundColor White
Write-Host "  Deployed: $($deployed.Count)" -ForegroundColor Green
Write-Host "  Failed: $($failed.Count)" -ForegroundColor $(if ($failed.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  Duration: $($duration.Minutes)m $($duration.Seconds)s" -ForegroundColor White
Write-Host ""

if ($deployed.Count -gt 0) {
    Write-Host "Deployed Services:" -ForegroundColor Cyan
    foreach ($dep in $deployed) {
        Write-Host "  $($dep.Name)" -ForegroundColor Green
        Write-Host "    URL: http://$($dep.Fqdn)" -ForegroundColor White
        Write-Host "    Swagger: http://$($dep.Fqdn)/swagger" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($failed.Count -gt 0) {
    Write-Host "Failed Services:" -ForegroundColor Red
    foreach ($fail in $failed) {
        Write-Host "  $fail" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "Management Commands:" -ForegroundColor Cyan
Write-Host "  List containers: az container list --resource-group $ResourceGroup --output table" -ForegroundColor White
Write-Host "  View logs: az container logs --name <container-name> --resource-group $ResourceGroup" -ForegroundColor White
Write-Host "  Delete container: az container delete --name <container-name> --resource-group $ResourceGroup --yes" -ForegroundColor White
Write-Host ""
Write-Host "Azure Portal:" -ForegroundColor Cyan
Write-Host "  https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroup/overview" -ForegroundColor White
Write-Host ""

if ($failed.Count -eq 0) {
    exit 0
} else {
    exit 1
}
