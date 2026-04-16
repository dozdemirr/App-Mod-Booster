#!/bin/bash
# deploy.sh
# Deploys the Expense Management System infrastructure and app code (NO GenAI resources)
# Deployment order follows prompt-023 best practices with 30-second wait
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - .NET 8 SDK installed (for building app.zip)
#   - Python 3 and pip3 installed
#   - ODBC Driver 18 for SQL Server installed
#
# Usage:
#   export ADMIN_OBJECT_ID="<your-entra-object-id>"
#   export ADMIN_LOGIN="<your-entra-upn>"
#   bash deploy.sh

set -e

# ──────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-expensemgmt-demo}"
LOCATION="${LOCATION:-swedencentral}"
DEPLOYMENT_NAME="expensemgmt-$(date +%Y%m%d%H%M)"

# Entra ID admin for SQL Server - SET THESE BEFORE RUNNING
ADMIN_OBJECT_ID="${ADMIN_OBJECT_ID:?Please export ADMIN_OBJECT_ID=<your-entra-object-id>}"
ADMIN_LOGIN="${ADMIN_LOGIN:?Please export ADMIN_LOGIN=<your-entra-upn e.g. you@example.com>}"

echo "=================================================="
echo " Expense Management System - Deployment Script"
echo "=================================================="
echo "  Resource Group : $RESOURCE_GROUP"
echo "  Location       : $LOCATION"
echo "  Admin Login    : $ADMIN_LOGIN"
echo "=================================================="

# ──────────────────────────────────────────────
# STEP 1: Create Resource Group
# ──────────────────────────────────────────────
echo ""
echo "[1/8] Creating resource group '$RESOURCE_GROUP'..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo "  ✓ Resource group ready"

# ──────────────────────────────────────────────
# STEP 2: Deploy Bicep Infrastructure
# ──────────────────────────────────────────────
echo ""
echo "[2/8] Deploying infrastructure (App Service + Managed Identity + Azure SQL)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "infra/main.bicep" \
    --parameters \
        adminObjectId="$ADMIN_OBJECT_ID" \
        adminLogin="$ADMIN_LOGIN" \
        deployGenAI=false \
    --query "properties.outputs" \
    --output json)

echo "  ✓ Infrastructure deployed"

# Extract outputs
APP_SERVICE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceName.value')
APP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appUrl.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
SQL_SERVER_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerName.value')
DATABASE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.databaseName.value')

echo "  App Service : $APP_SERVICE_NAME"
echo "  SQL Server  : $SQL_SERVER_FQDN"
echo "  MI Client ID: $MANAGED_IDENTITY_CLIENT_ID"

# ──────────────────────────────────────────────
# STEP 3: Configure App Service Settings
# ──────────────────────────────────────────────
echo ""
echo "[3/8] Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
    --name "$APP_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

az webapp config connection-string set \
    --name "$APP_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --connection-string-type SQLAzure \
    --settings "DefaultConnection=$CONNECTION_STRING" \
    --output none

echo "  ✓ App Service configured"

# ──────────────────────────────────────────────
# STEP 4: Wait 30 seconds for SQL Server to be fully ready
# ──────────────────────────────────────────────
echo ""
echo "[4/8] Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "  ✓ Wait complete"

# ──────────────────────────────────────────────
# STEP 5: Add current IP and Azure services to SQL Firewall
# ──────────────────────────────────────────────
echo ""
echo "[5/8] Configuring SQL Server firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)

# Allow Azure services access
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

# Add deployment IP (Codespaces / local machine)
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowDeploymentIP" \
    --start-ip-address "$MY_IP" \
    --end-ip-address "$MY_IP" \
    --output none

echo "  ✓ Firewall rules configured (Azure services + $MY_IP)"

echo "  Waiting 15 seconds for firewall rules to propagate..."
sleep 15

# ──────────────────────────────────────────────
# STEP 6: Install Python packages & Import database schema
# ──────────────────────────────────────────────
echo ""
echo "[6/8] Installing Python packages and importing database schema..."

pip3 install --quiet pyodbc azure-identity

# Set environment variables for Python scripts
export SQL_SERVER="$SQL_SERVER_FQDN"
export SQL_DATABASE="$DATABASE_NAME"
export MANAGED_IDENTITY_NAME="$MANAGED_IDENTITY_NAME"

python3 run-sql.py
echo "  ✓ Database schema imported"

# ──────────────────────────────────────────────
# STEP 7: Configure DB roles for managed identity
# ──────────────────────────────────────────────
echo ""
echo "[7/8] Configuring database roles for managed identity..."
python3 run-sql-dbrole.py
echo "  ✓ Database roles configured"

python3 run-sql-stored-procs.py
echo "  ✓ Stored procedures deployed"

# ──────────────────────────────────────────────
# STEP 8: Build and deploy the application code
# ──────────────────────────────────────────────
echo ""
echo "[8/8] Building and deploying application code..."

# Build the app
# Note: publish output must be outside the app directory to avoid nested directory issue
PUBLISH_DIR="$HOME/publish-out-$(date +%s)"
mkdir -p "$PUBLISH_DIR"
cd app
dotnet publish -c Release -o "$PUBLISH_DIR" --nologo -q
cd ..

# Create app.zip with DLL files at the root (Azure App Service requirement)
# Do NOT add a publish subfolder - files must be at zip root
cd "$PUBLISH_DIR"
zip -r "$OLDPWD/app.zip" . -x "*.pdb" -q
cd "$OLDPWD"
rm -rf "$PUBLISH_DIR"

echo "  ✓ app.zip created (DLL at zip root, no subdirectory)"

# Deploy to Azure App Service
az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_SERVICE_NAME" \
    --src-path ./app.zip \
    --type zip \
    --output none

echo "  ✓ Application deployed"

# ──────────────────────────────────────────────
# Done
# ──────────────────────────────────────────────
echo ""
echo "=================================================="
echo " ✅ DEPLOYMENT COMPLETE!"
echo "=================================================="
echo ""
echo "  🌐 App URL     : $APP_URL"
echo "     NOTE: Navigate to $APP_URL (the /Index path) to view the app."
echo "           The root URL redirects to /Index automatically."
echo ""
echo "  📊 SQL Server  : $SQL_SERVER_FQDN"
echo "  🔑 Identity    : $MANAGED_IDENTITY_NAME"
echo ""
echo "  📋 API Docs    : ${APP_URL%/Index}/swagger"
echo "  💬 AI Chat     : ${APP_URL%/Index}/chatui/index.html"
echo "     (Chat uses dummy AI responses - run deploy-with-chat.sh for full GenAI)"
echo ""
echo "  Local dev: Update app/appsettings.Development.json connection string"
echo "  and use 'Authentication=Active Directory Default' then run 'az login'"
echo ""
