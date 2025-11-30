#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys Yath microservices to Azure Kubernetes Service (AKS)
.DESCRIPTION
    Creates AKS cluster and deploys all services with RabbitMQ and Seq
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "yath-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$ClusterName = "yath-aks",
    
    [Parameter(Mandatory=$false)]
    [string]$AcrName = "yathregistry",
    
    [Parameter(Mandatory=$false)]
    [int]$NodeCount = 2,
    
    [Parameter(Mandatory=$false)]
    [string]$NodeSize = "Standard_B2s"
)

Write-Host "Deploying Yath to Azure Kubernetes Service" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

# Check Azure login
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) { throw "Not logged in" }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "Subscription: $($account.name)" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure!" -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  Cluster Name: $ClusterName" -ForegroundColor White
Write-Host "  Node Count: $NodeCount" -ForegroundColor White
Write-Host "  Node Size: $NodeSize (2 vCPU, 4GB RAM)" -ForegroundColor White
Write-Host ""

# Check if AKS cluster exists
$existing = az aks show --name $ClusterName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json

if ($null -eq $existing) {
    Write-Host "Creating AKS cluster (this will take 10-15 minutes)..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        az aks create `
            --resource-group $ResourceGroup `
            --name $ClusterName `
            --location $Location `
            --node-count $NodeCount `
            --node-vm-size $NodeSize `
            --enable-managed-identity `
            --generate-ssh-keys `
            --attach-acr $AcrName `
            --output none
        
        if ($LASTEXITCODE -ne 0) {
            throw "AKS cluster creation failed"
        }
        
        Write-Host "AKS cluster created successfully!" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host "Failed to create AKS cluster!" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "AKS cluster already exists" -ForegroundColor Green
    Write-Host ""
}

# Get AKS credentials
Write-Host "Getting AKS credentials..." -ForegroundColor Yellow
az aks get-credentials --resource-group $ResourceGroup --name $ClusterName --overwrite-existing --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to get AKS credentials!" -ForegroundColor Red
    exit 1
}

Write-Host "Credentials configured" -ForegroundColor Green
Write-Host ""

# Get ACR credentials for creating secret
Write-Host "Getting ACR credentials..." -ForegroundColor Yellow
$acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup --output json | ConvertFrom-Json
$acrLoginServer = "$AcrName.azurecr.io"
$acrUsername = $acrCreds.username
$acrPassword = $acrCreds.passwords[0].value

# Create docker-registry secret for ACR
$dockerConfigJson = @{
    auths = @{
        $acrLoginServer = @{
            username = $acrUsername
            password = $acrPassword
            auth = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("${acrUsername}:${acrPassword}"))
        }
    }
} | ConvertTo-Json -Depth 10 -Compress

$dockerConfigBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($dockerConfigJson))

Write-Host "ACR credentials configured" -ForegroundColor Green
Write-Host ""

# Deploy to Kubernetes
$kubeDir = Join-Path $PSScriptRoot "kubernetes"

Write-Host "Deploying to Kubernetes..." -ForegroundColor Yellow
Write-Host ""

# Create namespace
Write-Host "  Creating namespace..." -ForegroundColor Gray
kubectl apply -f (Join-Path $kubeDir "namespace.yaml")

# Create ACR secret
Write-Host "  Creating ACR secret..." -ForegroundColor Gray
kubectl delete secret acr-secret -n yath --ignore-not-found=true
kubectl create secret docker-registry acr-secret `
    --docker-server=$acrLoginServer `
    --docker-username=$acrUsername `
    --docker-password=$acrPassword `
    --namespace=yath

# Deploy infrastructure (RabbitMQ, Seq)
Write-Host "  Deploying RabbitMQ..." -ForegroundColor Gray
kubectl apply -f (Join-Path $kubeDir "rabbitmq.yaml")

Write-Host "  Deploying Seq..." -ForegroundColor Gray
kubectl apply -f (Join-Path $kubeDir "seq.yaml")

# Deploy services
$services = @("user-service", "trip-service", "activity-service", "expense-service", 
              "media-service", "chat-service", "location-service", "notification-service")

foreach ($service in $services) {
    $serviceFile = Join-Path $kubeDir "$service.yaml"
    if (Test-Path $serviceFile) {
        Write-Host "  Deploying $service..." -ForegroundColor Gray
        kubectl apply -f $serviceFile
    } else {
        Write-Host "  WARNING: $serviceFile not found, skipping" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host ""

Write-Host "Waiting for services to start (30 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host ""
Write-Host "Service Status:" -ForegroundColor Cyan
kubectl get pods -n yath
Write-Host ""

Write-Host "Service Endpoints:" -ForegroundColor Cyan
kubectl get services -n yath
Write-Host ""

Write-Host "Management Commands:" -ForegroundColor Cyan
Write-Host "  View pods:        kubectl get pods -n yath" -ForegroundColor Gray
Write-Host "  View services:    kubectl get services -n yath" -ForegroundColor Gray
Write-Host "  View logs:        kubectl logs -n yath <pod-name>" -ForegroundColor Gray
Write-Host "  Port forward:     kubectl port-forward -n yath service/user-service 8080:80" -ForegroundColor Gray
Write-Host "  Delete all:       kubectl delete namespace yath" -ForegroundColor Gray
Write-Host ""

Write-Host "Cost Estimate: ~`$150-200/month for 2-node cluster (24/7)" -ForegroundColor Yellow
Write-Host ""
