#!/bin/bash
# deploy-with-chat.sh
# Deploys everything including GenAI resources (Azure OpenAI + AI Search)
# for the Chat UI. Deploys GenAI first to get endpoints, then configures App Service.
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - .NET 8 SDK installed
#   - Python 3 and pip3 installed
#   - ODBC Driver 18 for SQL Server installed
#   - jq installed
#
# Usage:
#   export ADMIN_OBJECT_ID="<your-entra-object-id>"
#   export ADMIN_LOGIN="<your-entra-upn>"
#   bash deploy-with-chat.sh

set -e

# ──────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-expensemgmt-demo}"
LOCATION="${LOCATION:-swedencentral}"
DEPLOYMENT_NAME="expensemgmt-chat-$(date +%Y%m%d%H%M)"

ADMIN_OBJECT_ID="${ADMIN_OBJECT_ID:?Please export ADMIN_OBJECT_ID=<your-entra-object-id>}"
ADMIN_LOGIN="${ADMIN_LOGIN:?Please export ADMIN_LOGIN=<your-entra-upn e.g. you@example.com>}"

echo "========================================================"
echo " Expense Management System - Full Deployment (with Chat)"
echo "========================================================"
echo "  Resource Group : $RESOURCE_GROUP"
echo "  Location       : $LOCATION"
echo "  Admin Login    : $ADMIN_LOGIN"
echo "========================================================"

# ──────────────────────────────────────────────
# STEP 1: Create Resource Group
# ──────────────────────────────────────────────
echo ""
echo "[1/9] Creating resource group '$RESOURCE_GROUP'..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo "  ✓ Resource group ready"

# ──────────────────────────────────────────────
# STEP 2: Deploy ALL Infrastructure (including GenAI)
# ──────────────────────────────────────────────
echo ""
echo "[2/9] Deploying infrastructure (App Service + SQL + GenAI resources)..."
echo "      This includes Azure OpenAI (GPT-4o) and AI Search in swedencentral..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "infra/main.bicep" \
    --parameters \
        adminObjectId="$ADMIN_OBJECT_ID" \
        adminLogin="$ADMIN_LOGIN" \
        deployGenAI=true \
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

# GenAI endpoints (from deployGenAI=true outputs)
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value')

echo "  App Service     : $APP_SERVICE_NAME"
echo "  SQL Server      : $SQL_SERVER_FQDN"
echo "  OpenAI Endpoint : $OPENAI_ENDPOINT"
echo "  Search Endpoint : $SEARCH_ENDPOINT"

# ──────────────────────────────────────────────
# STEP 3: Configure App Service Settings (including GenAI)
# ──────────────────────────────────────────────
echo ""
echo "[3/9] Configuring App Service settings (including OpenAI endpoint)..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
    --name "$APP_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
        "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
        "Search__Endpoint=$SEARCH_ENDPOINT" \
    --output none

az webapp config connection-string set \
    --name "$APP_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --connection-string-type SQLAzure \
    --settings "DefaultConnection=$CONNECTION_STRING" \
    --output none

echo "  ✓ App Service configured with GenAI endpoints"

# ──────────────────────────────────────────────
# STEP 4: Wait 30 seconds for SQL Server
# ──────────────────────────────────────────────
echo ""
echo "[4/9] Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "  ✓ Wait complete"

# ──────────────────────────────────────────────
# STEP 5: SQL Firewall rules
# ──────────────────────────────────────────────
echo ""
echo "[5/9] Configuring SQL Server firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)

az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

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
# STEP 6: Python packages + database schema
# ──────────────────────────────────────────────
echo ""
echo "[6/9] Installing Python packages and importing database schema..."
pip3 install --quiet pyodbc azure-identity

export SQL_SERVER="$SQL_SERVER_FQDN"
export SQL_DATABASE="$DATABASE_NAME"
export MANAGED_IDENTITY_NAME="$MANAGED_IDENTITY_NAME"

python3 run-sql.py
echo "  ✓ Database schema imported"

# ──────────────────────────────────────────────
# STEP 7: DB roles + stored procedures
# ──────────────────────────────────────────────
echo ""
echo "[7/9] Configuring database roles and stored procedures..."
python3 run-sql-dbrole.py
echo "  ✓ Database roles configured"

python3 run-sql-stored-procs.py
echo "  ✓ Stored procedures deployed"

# ──────────────────────────────────────────────
# STEP 8: Build and deploy app code
# ──────────────────────────────────────────────
echo ""
echo "[8/9] Building and deploying application code..."

PUBLISH_DIR="$HOME/publish-out-$(date +%s)"
mkdir -p "$PUBLISH_DIR"
cd app
dotnet publish -c Release -o "$PUBLISH_DIR" --nologo -q
cd ..

# Create app.zip with DLL files at the root (Azure App Service requirement)
cd "$PUBLISH_DIR"
zip -r "$OLDPWD/app.zip" . -x "*.pdb" -q
cd "$OLDPWD"
rm -rf "$PUBLISH_DIR"

echo "  ✓ app.zip created (DLL at zip root, no subdirectory)"

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
echo "========================================================"
echo " ✅ FULL DEPLOYMENT COMPLETE (with GenAI / Chat)!"
echo "========================================================"
echo ""
echo "  🌐 App URL         : $APP_URL"
echo "     NOTE: Navigate to $APP_URL (the /Index path) to view the app."
echo ""
echo "  💬 AI Chat UI      : ${APP_URL%/Index}/chatui/index.html"
echo "  📋 API Docs        : ${APP_URL%/Index}/swagger"
echo "  📊 SQL Server      : $SQL_SERVER_FQDN"
echo "  🤖 OpenAI Endpoint : $OPENAI_ENDPOINT"
echo "  🔍 Search Endpoint : $SEARCH_ENDPOINT"
echo ""
echo "  Local dev: Update app/appsettings.Development.json and run 'az login'"
echo ""
