#!/usr/bin/env bash
set -euo pipefail

: "${RESOURCE_GROUP:?Set RESOURCE_GROUP}"
: "${LOCATION:=swedencentral}"
: "${ADMIN_OBJECT_ID:?Set ADMIN_OBJECT_ID (Entra object id)}"
: "${ADMIN_UPN:?Set ADMIN_UPN (Entra UPN)}"

MANAGED_IDENTITY_NAME="${MANAGED_IDENTITY_NAME:-mid-appmodassist-$(date +%d-%H-%M)}"

echo "Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "Deploying infrastructure including GenAI..."
az deployment group create \
  --name appmodassist-main \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters location=swedencentral \
               managedIdentityName="$MANAGED_IDENTITY_NAME" \
               adminObjectId="$ADMIN_OBJECT_ID" \
               adminLogin="$ADMIN_UPN" \
               deployGenAi=true \
  --output none

DEPLOYMENT_OUTPUT=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name appmodassist-main --query properties.outputs -o json)
APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceName.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
SQL_DB=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.databaseName.value')
MI_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value')

echo "Configuring app settings..."
az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --settings \
    "ManagedIdentityClientId=$MI_CLIENT_ID" \
    "AZURE_CLIENT_ID=$MI_CLIENT_ID" \
    "ConnectionStrings__SqlDatabase=Server=tcp:$SQL_SERVER_FQDN,1433;Database=$SQL_DB;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$MI_CLIENT_ID;" \
    "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
    "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    "Search__Endpoint=$SEARCH_ENDPOINT" \
  --output none

echo "Waiting 30 seconds for SQL Server readiness..."
sleep 30

echo "Adding current IP to SQL firewall..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)
export SQL_SERVER_FQDN
export SQL_DATABASE="$SQL_DB"

echo "Allowing Azure services access to SQL Server..."
az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "allowallazureips" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "allowdeploymentip" \
  --start-ip-address "$MY_IP" \
  --end-ip-address "$MY_IP" \
  --output none

echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

echo "Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

echo "Importing schema via Azure CLI..."
DB_TOKEN=$(az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
az sql db query \
  --server "$SQL_SERVER_NAME" \
  --database "$SQL_DB" \
  --access-token "$DB_TOKEN" \
  --file "Database-Schema/database_schema.sql"

cp script.sql script.generated.sql
sed -i.bak "s/MANAGED-IDENTITY/$MANAGED_IDENTITY_NAME/g" script.generated.sql && rm -f script.generated.sql.bak
mv script.generated.sql script.sql

python3 run-sql-dbrole.py
python3 run-sql-stored-procs.py
python3 run-sql.py

echo "Publishing app..."
dotnet publish AppModAssist/AppModAssist.csproj -c Release -o app
rm -f app.zip
(cd app && zip -r ../app.zip .)

echo "Deploying app.zip..."
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path ./app.zip \
  --output none

APP_URL=$(az webapp show --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" --query defaultHostName -o tsv)
echo "Deployment complete. Open https://$APP_URL/Index"
