#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Azure VM – Game Server Deployment (docker-compose)
#
# Prerequisites:
#   - Azure CLI installed and logged in  (az login)
#   - SSH key pair at ~/.ssh/id_rsa (or change SSH_KEY_PATH below)
#
# Usage:
#   chmod +x deploy.sh
#   ./deploy.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

# ── Variables (edit before running) ──────────────────────────────────────────
RESOURCE_GROUP="game-server-rg"
LOCATION="koreacentral"
VM_NAME="gameserver-vm"
VM_SIZE="Standard_D2s_v5"       # 2 vCPU, 8 GB RAM
ADMIN_USER="azureuser"
SSH_KEY_PATH="$HOME/.ssh/id_rsa.pub"
REPO_URL="https://github.com/Hellobot99/Project_charp_mmo.git"   # ← 레포 URL 입력
REPO_BRANCH="main"

# ── 1. Resource Group ─────────────────────────────────────────────────────────
echo ">>> Creating resource group: $RESOURCE_GROUP"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# ── 2. Create VM ──────────────────────────────────────────────────────────────
echo ">>> Creating VM: $VM_NAME ($VM_SIZE)"
az vm create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --image Ubuntu2204 \
  --size "$VM_SIZE" \
  --admin-username "$ADMIN_USER" \
  --ssh-key-values "$SSH_KEY_PATH" \
  --public-ip-sku Standard \
  --output table

# ── 3. Open ports ─────────────────────────────────────────────────────────────
echo ">>> Opening port 7000 (TCP game)"
az vm open-port \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --port 7000 \
  --priority 1000

echo ">>> Opening port 8080 (HTTP API)"
az vm open-port \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --port 8080 \
  --priority 1001

# ── 4. Get public IP ──────────────────────────────────────────────────────────
PUBLIC_IP=$(az vm show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --show-details \
  --query publicIps \
  --output tsv)

echo ">>> VM IP: $PUBLIC_IP"

# ── 5. Install Docker + docker-compose, clone repo, run ──────────────────────
echo ">>> Setting up server on VM..."
ssh -o StrictHostKeyChecking=no "$ADMIN_USER@$PUBLIC_IP" << EOF
  set -e

  # Docker 설치
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker $ADMIN_USER

  # docker-compose v2 설치
  sudo apt-get install -y docker-compose-plugin

  # 레포 클론
  git clone --branch $REPO_BRANCH $REPO_URL ~/game
  cd ~/game/Server

  # 실행
  sudo docker compose up --build -d

  echo "✅  Server started"
  sudo docker compose ps
EOF

echo ""
echo "✅  Deployment complete!"
echo "    Game server endpoint: $PUBLIC_IP:7000"
echo ""
echo "    SSH:  ssh $ADMIN_USER@$PUBLIC_IP"
echo "    Logs: ssh $ADMIN_USER@$PUBLIC_IP 'cd ~/game/Server && sudo docker compose logs -f gameserver'"
