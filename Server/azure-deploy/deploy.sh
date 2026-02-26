#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Azure Container Instances – Game Server Deployment
#
# Prerequisites:
#   - Azure CLI installed and logged in  (az login)
#   - Docker installed (for local build + push)
#
# Usage:
#   chmod +x deploy.sh
#   ./deploy.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

# ── Variables (edit before running) ──────────────────────────────────────────
RESOURCE_GROUP="game-server-rg"
LOCATION="koreacentral"           # e.g. eastus / koreacentral / japaneast
ACR_NAME="gameserveracr"          # must be globally unique, lowercase, 5-50 chars
ACI_NAME="gameserver-aci"
IMAGE_NAME="gameserver"
IMAGE_TAG="latest"

# External services – replace with your Azure Database for MySQL / Azure Cache for Redis endpoints
MYSQL_CONN="Server=<your-mysql>.mysql.database.azure.com;Port=3306;Database=gamedb;User Id=gameuser;Password=gamepassword;SslMode=Required;"
REDIS_CONN="<your-redis>.redis.cache.windows.net:6380,password=<access-key>,ssl=True,abortConnect=False"

# ── 1. Resource Group ─────────────────────────────────────────────────────────
echo ">>> Creating resource group: $RESOURCE_GROUP"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# ── 2. Azure Container Registry ───────────────────────────────────────────────
echo ">>> Creating ACR: $ACR_NAME"
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled true

# ── 3. Build & push image via ACR Tasks (no local Docker needed) ──────────────
echo ">>> Building image in ACR..."
az acr build \
  --registry "$ACR_NAME" \
  --image "$IMAGE_NAME:$IMAGE_TAG" \
  ..   # repo root (where Dockerfile lives)

# ── 4. Get ACR credentials ────────────────────────────────────────────────────
ACR_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer --output tsv)
ACR_USER=$(az acr credential show --name "$ACR_NAME" --query username --output tsv)
ACR_PASS=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" --output tsv)

# ── 5. Deploy to Azure Container Instances ────────────────────────────────────
echo ">>> Deploying ACI: $ACI_NAME"
az container create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACI_NAME" \
  --image "$ACR_SERVER/$IMAGE_NAME:$IMAGE_TAG" \
  --registry-login-server "$ACR_SERVER" \
  --registry-username "$ACR_USER" \
  --registry-password "$ACR_PASS" \
  --os-type Linux \
  --cpu 1 \
  --memory 1.5 \
  --ports 7000 \
  --protocol TCP \
  --restart-policy Always \
  --environment-variables \
    "ConnectionStrings__MySqlConnection=$MYSQL_CONN" \
    "ConnectionStrings__Redis=$REDIS_CONN" \
    "Server__Port=7000" \
    "Logging__LogLevel__Default=Information"

# ── 6. Show public IP ─────────────────────────────────────────────────────────
PUBLIC_IP=$(az container show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACI_NAME" \
  --query ipAddress.ip \
  --output tsv)

echo ""
echo "✅  Deployment complete!"
echo "    Game server endpoint: $PUBLIC_IP:7000"
