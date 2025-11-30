param(
    [string]$ResourceGroup = "yath-rg",
    [string]$Location = "eastus",
    [string]$AcrName = "yathregistry",
    [string]$VnetName = "yath-vnet",
    [string]$SubnetName = "yath-subnet"
)

Write-Host "Setting up Azure Infrastructure for Yath" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check Azure CLI
$azVersion = az version --output json 2>$null | ConvertFrom-Json
if (!$azVersion) {
    Write-Host "Azure CLI is not installed!" -ForegroundColor Red
    exit 1
}
Write-Host "Azure CLI is installed" -ForegroundColor Green

# Check login
$account = az account show --output json 2>$null | ConvertFrom-Json
if (!$account) {
    Write-Host "Not logged in. Running az login..." -ForegroundColor Yellow
    az login
    $account = az account show --output json | ConvertFrom-Json
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green

# Show configuration
Write-Host "`nConfiguration:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Location: $Location"
Write-Host "  ACR Name: $AcrName"

$continue = Read-Host "`nContinue? (Y/n)"
if ($continue -eq "n") { exit 0 }

# Create Resource Group
Write-Host "`nCreating Resource Group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Host "Resource group already exists" -ForegroundColor Green
}
else {
    az group create --name $ResourceGroup --location $Location --output none
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Resource group created" -ForegroundColor Green
    }
    else {
        Write-Host "Failed to create resource group" -ForegroundColor Red
        exit 1
    }
}

# Create ACR
Write-Host "`nCreating Azure Container Registry..." -ForegroundColor Yellow
$acrInfo = az acr show --name $AcrName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
if ($acrInfo) {
    Write-Host "ACR already exists" -ForegroundColor Green
}
else {
    Write-Host "Creating ACR (this may take a few minutes)..." -ForegroundColor Gray
    az acr create --name $AcrName --resource-group $ResourceGroup --location $Location --sku Basic --admin-enabled true --output none
    if ($LASTEXITCODE -eq 0) {
        $acrInfo = az acr show --name $AcrName --resource-group $ResourceGroup --output json | ConvertFrom-Json
        Write-Host "ACR created" -ForegroundColor Green
    }
    else {
        Write-Host "Failed to create ACR. Name may be taken." -ForegroundColor Red
        exit 1
    }
}

# Get ACR credentials
$acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup --output json | ConvertFrom-Json

# Create VNet
Write-Host "`nCreating Virtual Network..." -ForegroundColor Yellow
$vnetInfo = az network vnet show --name $VnetName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
if ($vnetInfo) {
    Write-Host "Virtual Network already exists" -ForegroundColor Green
}
else {
    az network vnet create --name $VnetName --resource-group $ResourceGroup --location $Location --address-prefix 10.0.0.0/16 --subnet-name $SubnetName --subnet-prefix 10.0.0.0/24 --output none
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Virtual Network created" -ForegroundColor Green
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Azure Infrastructure Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nResources Created:"
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Container Registry: $AcrName"
Write-Host "  Virtual Network: $VnetName"
Write-Host "`nACR Details:"
Write-Host "  Login Server: $($acrInfo.loginServer)"
Write-Host "  Username: $($acrCreds.username)"
Write-Host "`nNext Steps:"
Write-Host "  1. Push images: .\deploy-to-acr.ps1"
Write-Host "  2. Deploy to ACI: .\deploy-to-aci.ps1"
