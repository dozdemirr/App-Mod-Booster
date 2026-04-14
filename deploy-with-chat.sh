#!/usr/bin/env bash
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-appmodassist-demo}"
LOCATION="${LOCATION:-uksouth}"
DEPLOYMENT_NAME="${DEPLOYMENT_NAME:-deploy-appmodassist-chat}"
MANAGED_IDENTITY_NAME="${MANAGED_IDENTITY_NAME:-mid-appmodassist-$(date +%d-%H-%M)}"

ADMIN_OBJECT_ID="${ADMIN_OBJECT_ID:-$(az ad signed-in-user show --query id -o tsv)}"
ADMIN_LOGIN="${ADMIN_LOGIN:-$(az account show --query user.name -o tsv)}"

az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "Deploying infrastructure including GenAI resources..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-file infra/main.bicep \
  --parameters location="$LOCATION" adminObjectId="$ADMIN_OBJECT_ID" adminLogin="$ADMIN_LOGIN" managedIdentityName="$(echo "$MANAGED_IDENTITY_NAME" | tr '[:upper:]' '[:lower:]')" deployGenAi=true \
  --output none

APP_SERVICE_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.appServiceName.value" -o tsv)
MANAGED_IDENTITY_CLIENT_ID=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.managedIdentityClientId.value" -o tsv)
MANAGED_IDENTITY_NAME_EFFECTIVE=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.managedIdentityName.value" -o tsv)
SQL_SERVER_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.sqlServerName.value" -o tsv)
SQL_SERVER_FQDN=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.sqlServerFqdn.value" -o tsv)
SQL_DATABASE_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.sqlDatabaseName.value" -o tsv)
OPENAI_ENDPOINT=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.openAIEndpoint.value" -o tsv)
OPENAI_MODEL_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.openAIModelName.value" -o tsv)
SEARCH_ENDPOINT=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query "properties.outputs.searchEndpoint.value" -o tsv)

CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${SQL_DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

echo "Configuring app settings..."
az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --settings \
  "ConnectionStrings__SqlDb=$CONNECTION_STRING" \
  "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
  "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
  "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
  "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
  "Search__Endpoint=$SEARCH_ENDPOINT" \
  --output none

echo "Waiting 30 seconds for SQL Server readiness..."
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

pip3 install --quiet pyodbc azure-identity

az sql db query \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --database "$SQL_DATABASE_NAME" \
  --auth-mode ActiveDirectoryDefault \
  --file "Database-Schema/Northwinds Schema.sql" \
  --output none

sed -i.bak "s/SERVER = .*/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/DATABASE = .*/DATABASE = \"${SQL_DATABASE_NAME}\"/g" run-sql.py && rm -f run-sql.py.bak
python3 run-sql.py

sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME_EFFECTIVE}/g" script.sql && rm -f script.sql.bak
sed -i.bak "s/SERVER = .*/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/DATABASE = .*/DATABASE = \"${SQL_DATABASE_NAME}\"/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
python3 run-sql-dbrole.py

sed -i.bak "s/SERVER = .*/SERVER = \"${SQL_SERVER_FQDN}\"/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/DATABASE = .*/DATABASE = \"${SQL_DATABASE_NAME}\"/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
python3 run-sql-stored-procs.py

dotnet publish src/ModernExpenseApp/ModernExpenseApp.csproj -c Release -o app
rm -f app.zip
(cd app && zip -qr ../app.zip .)

az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --src-path ./app.zip \
  --type zip \
  --output none

echo "Done. Open app: https://${APP_SERVICE_NAME}.azurewebsites.net/Index"
echo "Chat UI: https://${APP_SERVICE_NAME}.azurewebsites.net/Chat"
