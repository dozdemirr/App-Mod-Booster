#!/usr/bin/env bash
set -euo pipefail

LOCATION=${LOCATION:-uksouth}
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-appmodassist-demo}
WORKLOAD_NAME=${WORKLOAD_NAME:-appmodassist}

az account show >/dev/null

ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=$(az account show --query user.name -o tsv)

az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters workloadName="$WORKLOAD_NAME" adminObjectId="$ADMIN_OBJECT_ID" adminLogin="$ADMIN_LOGIN" deployGenAI=true \
  --query properties.outputs -o json)

APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appName.value')
APP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appUrl.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
SQL_DB_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlDatabaseName.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value')

CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${SQL_DB_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --settings \
  "AZURE_CLIENT_ID=${MANAGED_IDENTITY_CLIENT_ID}" \
  "ManagedIdentityClientId=${MANAGED_IDENTITY_CLIENT_ID}" \
  "ConnectionStrings__DefaultConnection=${CONNECTION_STRING}" \
  "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
  "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
  "AISearch__Endpoint=$SEARCH_ENDPOINT" \
  --output none

echo "Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# Add current IP to SQL firewall
echo "Adding current IP to SQL firewall..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)

# Allow Azure services access
echo "Allowing Azure services access to SQL Server..."
az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowAllAzureIPs" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none

# Add deployment IP
az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowDeploymentIP" \
  --start-ip-address "$MY_IP" \
  --end-ip-address "$MY_IP" \
  --output none

echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

# Install required Python packages if not already installed
pip3 install --quiet pyodbc azure-identity

sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak
python3 run-sql.py
python3 run-sql-dbrole.py
python3 run-sql-stored-procs.py

dotnet publish AppModAssist/AppModAssist.csproj -c Release -o app
(cd app && zip -r ../app.zip .)

az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path ./app.zip

echo "Deployment complete with chat services. Open ${APP_URL}/Index"
