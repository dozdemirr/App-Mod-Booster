#!/bin/bash
set -e

# ============================================================
# Expense Management App - Full Deployment (With GenAI/Chat)
# Usage: ./deploy-with-chat.sh
# ============================================================

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-expenseapp}"
LOCATION="${LOCATION:-uksouth}"

echo "============================================================"
echo " Expense Management App - Full Deployment (with GenAI)"
echo "============================================================"

# ---- 1. Ensure logged in ----
echo "[1/10] Checking Azure CLI login..."
az account show --output none 2>/dev/null || { echo "Please run: az login"; exit 1; }
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "Using subscription: $SUBSCRIPTION_ID"

# ---- 2. Get Entra ID info ----
echo "[2/10] Getting Entra ID admin info..."
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
echo "Admin: $ADMIN_LOGIN ($ADMIN_OBJECT_ID)"

# ---- 3. Create resource group ----
echo "[3/10] Creating resource group: $RESOURCE_GROUP..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

# ---- 4. Deploy all Bicep (App Service + SQL + GenAI) ----
echo "[4/10] Deploying all infrastructure (App Service + SQL + GenAI)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters adminObjectId="$ADMIN_OBJECT_ID" adminLogin="$ADMIN_LOGIN" deployGenAI=true \
  --query properties.outputs \
  --output json)

APP_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')
MANAGED_IDENTITY_NAME="mid-AppModAssist-14-30-00"

echo "App Service: $APP_NAME"
echo "App URL: $APP_URL"
echo "SQL Server: $SQL_SERVER_FQDN"
echo "OpenAI Endpoint: $OPENAI_ENDPOINT"

# ---- 5. Configure App Service settings ----
echo "[5/10] Configuring App Service settings..."
SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN};Database=Northwind;Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config appsettings set \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "ConnectionStrings__DefaultConnection=${SQL_CONNECTION_STRING}" \
    "AZURE_CLIENT_ID=${MANAGED_IDENTITY_CLIENT_ID}" \
    "ManagedIdentityClientId=${MANAGED_IDENTITY_CLIENT_ID}" \
    "OpenAI__Endpoint=${OPENAI_ENDPOINT}" \
    "OpenAI__DeploymentName=${OPENAI_MODEL_NAME}" \
    "Search__Endpoint=${SEARCH_ENDPOINT}" \
  --output none

echo "Waiting 30 seconds for SQL Server to be ready..."
sleep 30

# ---- 6. Add firewall rules ----
echo "[6/10] Adding SQL firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo $SQL_SERVER_FQDN | cut -d'.' -f1)
az sql server firewall-rule create --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER_NAME" --name "AllowAllAzureIPs" --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --output none
az sql server firewall-rule create --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER_NAME" --name "AllowDeploymentIP" --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" --output none
echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

# ---- 7. Install Python dependencies ----
echo "[7/10] Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# ---- 8. Run database scripts ----
echo "[8/10] Running database scripts..."
export SQL_SERVER_FQDN
export MANAGED_IDENTITY_NAME

echo "  → Importing database schema..."
python3 run-sql.py

echo "  → Configuring database roles..."
MANAGED_IDENTITY_NAME_EXPORT="$MANAGED_IDENTITY_NAME"
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME_EXPORT/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py

echo "  → Deploying stored procedures..."
python3 run-sql-stored-procs.py

# ---- 9. Deploy application code ----
echo "[9/10] Deploying application code..."
cd src
dotnet publish -c Release -o ./publish --nologo
cd publish
zip -r ../../app.zip . -x "*.pdb"
cd ../..

az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path ./app.zip \
  --type zip \
  --output none

echo ""
echo "============================================================"
echo " Full Deployment Complete!"
echo "============================================================"
echo " App URL: ${APP_URL}/Index"
echo " SQL Server: $SQL_SERVER_FQDN"
echo " OpenAI Endpoint: $OPENAI_ENDPOINT"
echo " Search Endpoint: $SEARCH_ENDPOINT"
echo "============================================================"
