# Azure Deployment Guide - Yath Microservices

This guide explains how to deploy Yath microservices to Azure using Azure Container Registry (ACR) and Azure Container Instances (ACI).

## Prerequisites

1. **Azure Account**: Active Azure subscription ([Create free account](https://azure.microsoft.com/free/))
2. **Azure CLI**: Version 2.0 or later ([Installation guide](https://docs.microsoft.com/cli/azure/install-azure-cli))
3. **Docker Desktop**: Running locally with all images built
4. **PowerShell**: Version 5.1 or PowerShell Core 7+

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Azure Cloud                             │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Azure Container Registry (ACR)                      │  │
│  │  - yath-user-service:latest                          │  │
│  │  - yath-trip-service:latest                          │  │
│  │  - yath-activity-service:latest                      │  │
│  │  - yath-expense-service:latest                       │  │
│  │  - yath-media-service:latest                         │  │
│  │  - yath-chat-service:latest                          │  │
│  │  - yath-location-service:latest                      │  │
│  │  - yath-notification-service:latest                  │  │
│  └──────────────────────────────────────────────────────┘  │
│                            ↓                                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Azure Container Instances (ACI)                     │  │
│  │                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐ │  │
│  │  │ User        │  │ Trip        │  │ Activity     │ │  │
│  │  │ Service     │  │ Service     │  │ Service      │ │  │
│  │  │ :5000       │  │ :5001       │  │ :5002        │ │  │
│  │  └─────────────┘  └─────────────┘  └──────────────┘ │  │
│  │                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐ │  │
│  │  │ Expense     │  │ Media       │  │ Chat         │ │  │
│  │  │ Service     │  │ Service     │  │ Service      │ │  │
│  │  │ :5003       │  │ :5004       │  │ :5005        │ │  │
│  │  └─────────────┘  └─────────────┘  └──────────────┘ │  │
│  │                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐                   │  │
│  │  │ Location    │  │Notification │                   │  │
│  │  │ Service     │  │ Service     │                   │  │
│  │  │ :5006       │  │ :5007       │                   │  │
│  │  └─────────────┘  └─────────────┘                   │  │
│  │                                                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start (5 Minutes)

### Step 1: Build Docker Images Locally

```powershell
cd C:\Users\seshathp\source\repos\Omni\Yath
.\build-images.ps1
```

**Expected Output:**
```
✓ All images built successfully!
  Total services: 8
  Duration: ~5-10 minutes
```

### Step 2: Setup Azure Infrastructure

```powershell
.\setup-azure-infrastructure.ps1
```

**What it does:**
- Creates Azure Resource Group: `yath-rg`
- Creates Azure Container Registry: `yathregistry` (customize if needed)
- Creates Virtual Network: `yath-vnet`
- Enables admin access for ACR

**Parameters (Optional):**
```powershell
.\setup-azure-infrastructure.ps1 `
    -ResourceGroup "my-rg" `
    -Location "centralindia" `
    -AcrName "myuniquename123"
```

**Important:** ACR names must be globally unique. If `yathregistry` is taken, use a different name.

### Step 3: Push Images to ACR

```powershell
.\deploy-to-acr.ps1
```

**What it does:**
- Logs in to Azure Container Registry
- Tags all 8 local Docker images
- Pushes images to ACR (~5-10 minutes depending on connection)

**Expected Output:**
```
✓ All images pushed successfully!
  Total services: 8
  Successful: 8
  Failed: 0
```

### Step 4: Deploy to Azure Container Instances

```powershell
.\deploy-to-aci.ps1
```

**What it does:**
- Creates 8 Azure Container Instances
- Configures environment variables
- Sets up DNS names for each service
- Exposes services on public URLs

**Expected Output:**
```
✓ All services deployed successfully!
  ✓ yath-user-service
    URL: http://yath-user-service.eastus.azurecontainer.io
    Swagger: http://yath-user-service.eastus.azurecontainer.io/swagger
  ...
```

## Detailed Configuration

### Custom Resource Group & Location

```powershell
# Setup with custom parameters
.\setup-azure-infrastructure.ps1 `
    -ResourceGroup "yath-production" `
    -Location "southeastasia" `
    -AcrName "yathprodregistry"

# Push to custom ACR
.\deploy-to-acr.ps1 `
    -ResourceGroup "yath-production" `
    -AcrName "yathprodregistry"

# Deploy to custom location
.\deploy-to-aci.ps1 `
    -ResourceGroup "yath-production" `
    -Location "southeastasia" `
    -AcrName "yathprodregistry"
```

### Azure Regions (Location)

Choose the closest region for best performance:

| Region | Location Code | Description |
|--------|--------------|-------------|
| East US | `eastus` | Virginia, USA |
| West US 2 | `westus2` | Washington, USA |
| Central India | `centralindia` | Pune, India |
| Southeast Asia | `southeastasia` | Singapore |
| West Europe | `westeurope` | Netherlands |
| UK South | `uksouth` | London, UK |

### MongoDB Configuration

The default configuration uses `mongodb://admin:admin123@mongodb:27017`. For production:

**Option 1: Azure Cosmos DB (Recommended)**
```powershell
.\deploy-to-aci.ps1 `
    -MongoConnectionString "mongodb://your-cosmos-account:your-key@your-cosmos-account.mongo.cosmos.azure.com:10255/?ssl=true"
```

**Option 2: MongoDB Atlas**
```powershell
.\deploy-to-aci.ps1 `
    -MongoConnectionString "mongodb+srv://username:password@cluster.mongodb.net/yath?retryWrites=true"
```

**Option 3: Deploy MongoDB as ACI**
```powershell
az container create `
    --name mongodb `
    --resource-group yath-rg `
    --image mongo:7.0 `
    --dns-name-label yath-mongodb `
    --ports 27017 `
    --cpu 1 `
    --memory 2 `
    --environment-variables MONGO_INITDB_ROOT_USERNAME=admin MONGO_INITDB_ROOT_PASSWORD=admin123
```

Then update connection string:
```powershell
.\deploy-to-aci.ps1 `
    -MongoConnectionString "mongodb://admin:admin123@yath-mongodb.eastus.azurecontainer.io:27017"
```

## Service URLs

After deployment, each service gets a unique FQDN:

| Service | Default URL Pattern |
|---------|-------------------|
| User Service | `http://yath-user-service.{location}.azurecontainer.io` |
| Trip Service | `http://yath-trip-service.{location}.azurecontainer.io` |
| Activity Service | `http://yath-activity-service.{location}.azurecontainer.io` |
| Expense Service | `http://yath-expense-service.{location}.azurecontainer.io` |
| Media Service | `http://yath-media-service.{location}.azurecontainer.io` |
| Chat Service | `http://yath-chat-service.{location}.azurecontainer.io` |
| Location Service | `http://yath-location-service.{location}.azurecontainer.io` |
| Notification Service | `http://yath-notification-service.{location}.azurecontainer.io` |

**Swagger UI**: Append `/swagger` to any service URL

## Management Commands

### View All Containers

```powershell
az container list --resource-group yath-rg --output table
```

### View Container Logs

```powershell
# View logs for a specific service
az container logs --name yath-user-service --resource-group yath-rg

# Follow logs in real-time
az container logs --name yath-user-service --resource-group yath-rg --follow
```

### Restart Container

```powershell
az container restart --name yath-user-service --resource-group yath-rg
```

### Delete Container

```powershell
az container delete --name yath-user-service --resource-group yath-rg --yes
```

### Update Container (Redeploy)

```powershell
# Delete old container
az container delete --name yath-user-service --resource-group yath-rg --yes

# Push new image
docker tag yath-user-service:latest yathregistry.azurecr.io/yath-user-service:latest
docker push yathregistry.azurecr.io/yath-user-service:latest

# Redeploy (script will recreate)
.\deploy-to-aci.ps1
```

### Scale Resources

To change CPU/Memory allocation, update `deploy-to-aci.ps1`:

```powershell
$services = @(
    @{ Name = "user-service"; Port = 5000; Cpu = 2.0; Memory = 3.0 },  # Increased
    # ...
)
```

## Cost Optimization

### Azure Container Instances Pricing (East US)

**Per Container:**
- vCPU: $0.0000012 per second ($0.0432/hour)
- Memory: $0.0000001333 per GB/second ($0.0048/GB/hour)

**Example Monthly Costs (24/7 running):**

| Configuration | vCPU | RAM | Cost/Month |
|--------------|------|-----|------------|
| Small Service | 0.5 | 1 GB | ~$19 |
| Medium Service | 1.0 | 1.5 GB | ~$37 |
| Large Service | 2.0 | 3 GB | ~$77 |

**Total for 8 Services (Default Config):** ~$250-300/month

### Cost Reduction Strategies

1. **Stop non-production services**:
   ```powershell
   az container stop --name yath-activity-service --resource-group yath-rg
   ```

2. **Use Azure Container Apps** (serverless, scales to zero):
   - Better for production workloads
   - Automatic scaling
   - Built-in ingress

3. **Use Azure Kubernetes Service (AKS)**:
   - Lower cost for multiple containers
   - Better resource sharing
   - More control over infrastructure

## Monitoring & Observability

### Azure Portal

1. Go to: https://portal.azure.com
2. Navigate to Resource Groups → `yath-rg`
3. Click on any container instance
4. View:
   - Metrics (CPU, Memory usage)
   - Logs
   - Container properties

### Application Insights (Optional)

Add to environment variables in `deploy-to-aci.ps1`:

```powershell
{"name": "APPLICATIONINSIGHTS_CONNECTION_STRING", "value": "your-connection-string"}
```

### Seq Logging (Currently Local)

To use Seq in Azure:

```powershell
# Deploy Seq as ACI
az container create `
    --name seq `
    --resource-group yath-rg `
    --image datalust/seq:latest `
    --dns-name-label yath-seq `
    --ports 80 5341 `
    --cpu 1 `
    --memory 2 `
    --environment-variables ACCEPT_EULA=Y

# Update services
.\deploy-to-aci.ps1 -SeqUrl "http://yath-seq.eastus.azurecontainer.io:5341"
```

## Troubleshooting

### Issue: ACR Name Already Taken

**Error:** `The registry name 'yathregistry' is already in use.`

**Solution:**
```powershell
.\setup-azure-infrastructure.ps1 -AcrName "yath$(Get-Random -Maximum 9999)"
```

### Issue: Container Won't Start

**Check logs:**
```powershell
az container logs --name yath-user-service --resource-group yath-rg
```

**Common causes:**
- Invalid MongoDB connection string
- Missing environment variables
- Port conflicts

### Issue: Can't Access Service URL

**Verify container is running:**
```powershell
az container show --name yath-user-service --resource-group yath-rg --query "provisioningState"
```

**Check IP and FQDN:**
```powershell
az container show --name yath-user-service --resource-group yath-rg --query "ipAddress"
```

### Issue: Out of Memory

**Increase memory allocation** in `deploy-to-aci.ps1`:
```powershell
@{ Name = "user-service"; Port = 5000; Cpu = 1.0; Memory = 2.0 }  # Was 1.5
```

## Security Best Practices

### 1. Use Managed Identities

Instead of ACR passwords:
```powershell
# Enable system-assigned identity
az container create --assign-identity

# Grant ACR pull permissions
az role assignment create --assignee <identity> --role AcrPull
```

### 2. Use Azure Key Vault for Secrets

```powershell
# Create Key Vault
az keyvault create --name yath-keyvault --resource-group yath-rg

# Store secrets
az keyvault secret set --vault-name yath-keyvault --name MongoDbConnectionString --value "mongodb://..."

# Reference in container
--secrets KEY_VAULT_SECRET_URI=<secret-uri>
```

### 3. Network Isolation

Deploy containers in a virtual network:
```powershell
az container create --vnet $VnetName --subnet $SubnetName
```

### 4. Use Private ACR Endpoint

```powershell
az acr update --name $AcrName --public-network-enabled false
```

## Cleanup

### Delete All Containers

```powershell
az container list --resource-group yath-rg --query "[].name" -o tsv | ForEach-Object {
    az container delete --name $_ --resource-group yath-rg --yes
}
```

### Delete Resource Group (Everything)

```powershell
az group delete --name yath-rg --yes --no-wait
```

### Keep ACR, Delete Containers Only

```powershell
az container list --resource-group yath-rg --query "[].name" -o tsv | ForEach-Object {
    az container delete --name $_ --resource-group yath-rg --yes
}
```

## Next Steps

- [ ] Set up CI/CD with GitHub Actions
- [ ] Configure custom domains with Azure DNS
- [ ] Implement Azure Front Door for global load balancing
- [ ] Set up Azure Monitor alerts
- [ ] Configure auto-scaling with Azure Container Apps
- [ ] Implement blue-green deployments

## Support

- **Azure Documentation**: https://docs.microsoft.com/azure/container-instances/
- **ACR Documentation**: https://docs.microsoft.com/azure/container-registry/
- **GitHub Issues**: Report issues in the repository

## License

This deployment configuration is part of the Yath project.
