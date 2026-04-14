#!/bin/bash
set -e

# =============================================================================
# Variables - SET THESE BEFORE RUNNING
# =============================================================================
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="swedencentral"
ADMIN_LOGIN="<your-azure-ad-upn@domain.com>"
ADMIN_OBJECT_ID="<your-azure-ad-object-id>"

# =============================================================================
# Step 1: Create resource group
# =============================================================================
echo "Step 1: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION

# =============================================================================
# Step 2: Deploy main Bicep (with GenAI)
# =============================================================================
echo "Step 2: Deploying main Bicep template (with GenAI)..."
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --template-file infra/main.bicep \
  --parameters location=$LOCATION adminLogin="$ADMIN_LOGIN" adminObjectId="$ADMIN_OBJECT_ID" deployGenAI=true \
  --output json

# =============================================================================
# Step 3: Capture deployment outputs
# =============================================================================
echo "Step 3: Capturing deployment outputs..."
APP_SERVICE_NAME=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.appServiceName.value" -o tsv)
SQL_SERVER_FQDN=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.sqlServerFqdn.value" -o tsv)
SQL_SERVER_NAME=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.sqlServerName.value" -o tsv)
DATABASE_NAME=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.databaseName.value" -o tsv)
MANAGED_IDENTITY_CLIENT_ID=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.managedIdentityClientId.value" -o tsv)
APP_SERVICE_URL=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.appServiceUrl.value" -o tsv)
OPENAI_ENDPOINT=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.openAIEndpoint.value" -o tsv)
OPENAI_MODEL_NAME=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.openAIModelName.value" -o tsv)
SEARCH_ENDPOINT=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query "properties.outputs.searchEndpoint.value" -o tsv)

echo "  App Service:     $APP_SERVICE_NAME"
echo "  SQL Server:      $SQL_SERVER_NAME"
echo "  Database:        $DATABASE_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  Search Endpoint: $SEARCH_ENDPOINT"

# =============================================================================
# Step 4: Configure App Service settings
# =============================================================================
echo "Step 4: Configuring App Service settings..."
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --settings \
    "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    "ConnectionStrings__DefaultConnection=Server=tcp:$SQL_SERVER_FQDN,1433;Database=$DATABASE_NAME;Authentication=Active Directory Managed Identity;User Id=$MANAGED_IDENTITY_CLIENT_ID;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
    "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
    "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    "Search__Endpoint=$SEARCH_ENDPOINT"

# =============================================================================
# Step 5: Wait 30 seconds for SQL Server to be ready
# =============================================================================
echo "Step 5: Waiting 30 seconds for SQL Server to be ready..."
sleep 30

# =============================================================================
# Step 6: Add current IP to SQL firewall
# =============================================================================
echo "Step 6: Configuring SQL firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name "AllowAllAzureIPs" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name "AllowDeploymentIP" \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP \
  --output none
echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

# =============================================================================
# Step 7: Update SQL scripts with actual server name
# =============================================================================
echo "Step 7: Updating SQL scripts with server name..."
sed -i.bak "s|<SQL_SERVER_FQDN>|$SQL_SERVER_FQDN|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|<SQL_SERVER_FQDN>|$SQL_SERVER_FQDN|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|<SQL_SERVER_FQDN>|$SQL_SERVER_FQDN|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# =============================================================================
# Step 8: Install Python dependencies
# =============================================================================
echo "Step 8: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# =============================================================================
# Step 9: Import database schema
# =============================================================================
echo "Step 9: Importing database schema..."
python3 run-sql.py

# =============================================================================
# Step 10: Grant managed identity DB roles
# =============================================================================
echo "Step 10: Granting managed identity DB roles..."
MANAGED_IDENTITY_NAME="mid-AppModAssist-14-16-40"
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py

# =============================================================================
# Step 11: Deploy stored procedures
# =============================================================================
echo "Step 11: Deploying stored procedures..."
python3 run-sql-stored-procs.py

# =============================================================================
# Step 12: Build and package the app
# =============================================================================
echo "Step 12: Building and packaging the app..."
cd app/ExpenseManagement
dotnet publish -c Release -o ./publish
cd ./publish
zip -r ../app.zip .
cd ..
rm -rf ./publish
cd ../..

# =============================================================================
# Step 13: Deploy app to Azure App Service
# =============================================================================
echo "Step 13: Deploying app to Azure App Service..."
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --src-path ./app/ExpenseManagement/app.zip \
  --type zip

# =============================================================================
# Step 14: Print success message
# =============================================================================
echo ""
echo "✅ Deployment complete (with GenAI / Chat)!"
echo "   App URL: https://$APP_SERVICE_URL/Index"
echo "   NOTE: Navigate to https://$APP_SERVICE_URL/Index (not the root URL)"
echo ""
echo "   Chat UI: https://$APP_SERVICE_URL/Chat"
echo "   ✅ Chat is fully functional with Azure OpenAI and AI Search."
