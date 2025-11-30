#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys all Yath services + sidecars as a single container group in ACI
.DESCRIPTION
    Creates one container group with all 8 microservices, RabbitMQ, and Seq logging
    Services can communicate via localhost
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "yath-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$AcrName = "yathregistry",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerGroupName = "yath-services"
)

Write-Host "Deploying Yath Microservices as Single Container Group" -ForegroundColor Cyan
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

Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  ACR: $acrLoginServer" -ForegroundColor White
Write-Host "  Container Group: $ContainerGroupName" -ForegroundColor White
Write-Host ""

# Delete existing container group if exists
$existing = az container show --name $ContainerGroupName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
if ($null -ne $existing) {
    Write-Host "Deleting existing container group..." -ForegroundColor Yellow
    az container delete --name $ContainerGroupName --resource-group $ResourceGroup --yes --output none
    Start-Sleep -Seconds 10
}

Write-Host "Creating container group with all services..." -ForegroundColor Yellow
Write-Host ""

# Create YAML deployment file
$yamlContent = @"
apiVersion: '2021-09-01'
location: $Location
name: $ContainerGroupName
properties:
  containers:
  # RabbitMQ - Message Bus
  - name: rabbitmq
    properties:
      image: $acrLoginServer/rabbitmq:latest
      resources:
        requests:
          cpu: 0.3
          memoryInGb: 0.5
      ports:
      - protocol: tcp
        port: 5672
      - protocol: tcp
        port: 15672
      environmentVariables:
      - name: RABBITMQ_DEFAULT_USER
        value: guest
      - name: RABBITMQ_DEFAULT_PASS
        value: guest
  
  # Seq - Logging
  - name: seq
    properties:
      image: $acrLoginServer/seq:latest
      resources:
        requests:
          cpu: 0.2
          memoryInGb: 0.5
      ports:
      - protocol: tcp
        port: 5341
      - protocol: tcp
        port: 80
      environmentVariables:
      - name: ACCEPT_EULA
        value: Y
  
  # User Service
  - name: user-service
    properties:
      image: $acrLoginServer/yath-user-service:latest
      resources:
        requests:
          cpu: 0.5
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5000
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5000
  
  # Trip Service
  - name: trip-service
    properties:
      image: $acrLoginServer/yath-trip-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5001
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5001
  
  # Activity Service
  - name: activity-service
    properties:
      image: $acrLoginServer/yath-activity-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5002
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5002
  
  # Expense Service
  - name: expense-service
    properties:
      image: $acrLoginServer/yath-expense-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5003
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5003
  
  # Media Service
  - name: media-service
    properties:
      image: $acrLoginServer/yath-media-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5004
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5004
  
  # Chat Service
  - name: chat-service
    properties:
      image: $acrLoginServer/yath-chat-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5005
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5005
  
  # Location Service
  - name: location-service
    properties:
      image: $acrLoginServer/yath-location-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5006
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5006
  
  # Notification Service
  - name: notification-service
    properties:
      image: $acrLoginServer/yath-notification-service:latest
      resources:
        requests:
          cpu: 0.4
          memoryInGb: 0.8
      ports:
      - protocol: tcp
        port: 5007
      environmentVariables:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://+:5007
  
  imageRegistryCredentials:
  - server: $acrLoginServer
    username: $acrUsername
    password: $acrPassword
  
  ipAddress:
    type: Public
    dnsNameLabel: $ContainerGroupName
    ports:
    - protocol: tcp
      port: 5000
    - protocol: tcp
      port: 5001
    - protocol: tcp
      port: 5002
    - protocol: tcp
      port: 5003
    - protocol: tcp
      port: 5004
    - protocol: tcp
      port: 5005
    - protocol: tcp
      port: 5006
    - protocol: tcp
      port: 5007
    - protocol: tcp
      port: 15672
    - protocol: tcp
      port: 80
  
  osType: Linux
  restartPolicy: Always

tags:
  environment: production
  project: yath
type: Microsoft.ContainerInstance/containerGroups
"@

$yamlFile = Join-Path $PSScriptRoot "container-group.yaml"
$yamlContent | Out-File -FilePath $yamlFile -Encoding utf8 -Force

Write-Host "YAML file created at: $yamlFile" -ForegroundColor Gray
Write-Host "Deploying container group (this will take 5-10 minutes)..." -ForegroundColor Yellow

try {
    az container create `
        --resource-group $ResourceGroup `
        --file $yamlFile `
        --output json
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host ("=" * 60) -ForegroundColor Green
        Write-Host "Container group deployed successfully!" -ForegroundColor Green
        Write-Host ("=" * 60) -ForegroundColor Green
        Write-Host ""
        
        $containerInfo = az container show --name $ContainerGroupName --resource-group $ResourceGroup --output json | ConvertFrom-Json
        $fqdn = $containerInfo.ipAddress.fqdn
        $ip = $containerInfo.ipAddress.ip
        
        Write-Host "Container Group Details:" -ForegroundColor Cyan
        Write-Host "  Name: $ContainerGroupName" -ForegroundColor White
        Write-Host "  FQDN: $fqdn" -ForegroundColor White
        Write-Host "  IP: $ip" -ForegroundColor White
        Write-Host ""
        
        Write-Host "Service URLs:" -ForegroundColor Cyan
        Write-Host "  User Service:         http://${fqdn}:5000/swagger" -ForegroundColor White
        Write-Host "  Trip Service:         http://${fqdn}:5001/swagger" -ForegroundColor White
        Write-Host "  Activity Service:     http://${fqdn}:5002/swagger" -ForegroundColor White
        Write-Host "  Expense Service:      http://${fqdn}:5003/swagger" -ForegroundColor White
        Write-Host "  Media Service:        http://${fqdn}:5004/swagger" -ForegroundColor White
        Write-Host "  Chat Service:         http://${fqdn}:5005/swagger" -ForegroundColor White
        Write-Host "  Location Service:     http://${fqdn}:5006/swagger" -ForegroundColor White
        Write-Host "  Notification Service: http://${fqdn}:5007/swagger" -ForegroundColor White
        Write-Host ""
        Write-Host "  RabbitMQ Management:  http://${fqdn}:15672 (guest/guest)" -ForegroundColor White
        Write-Host "  Seq Logs:             http://${fqdn}:80" -ForegroundColor White
        Write-Host ""
        
        Write-Host "Management Commands:" -ForegroundColor Cyan
        Write-Host "  View logs: az container logs --name $ContainerGroupName --resource-group $ResourceGroup --container-name <container-name>" -ForegroundColor Gray
        Write-Host "  Delete: az container delete --name $ContainerGroupName --resource-group $ResourceGroup --yes" -ForegroundColor Gray
        Write-Host ""
        
        Write-Host "Note: Total resources: 3.7 CPU cores, 6.9 GB RAM" -ForegroundColor Yellow
        Write-Host "      Estimated cost: ~`$100-120/month for 24/7 operation" -ForegroundColor Yellow
        
    } else {
        throw "Container group creation failed"
    }
    
} catch {
    Write-Host ""
    Write-Host "Deployment failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Keep YAML file for debugging
    # if (Test-Path $yamlFile) {
    #     Remove-Item $yamlFile -Force
    # }
}
