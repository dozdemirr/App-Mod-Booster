// app-service.bicep
// Deploys App Service Plan + App Service + User-Assigned Managed Identity
// Sweden Central, S1 SKU

param location string = 'swedencentral'
param appName string = toLower('app-expensemgmt-${uniqueString(resourceGroup().id)}')
param planName string = toLower('plan-expensemgmt-${uniqueString(resourceGroup().id)}')
param managedIdentityName string = 'mid-AppModAssist-16-10-15'

// User-Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// App Service Plan - S1 SKU to avoid cold starts
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentity.properties.clientId
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentity.properties.clientId
        }
      ]
    }
  }
}

output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
// principalId is on properties of the managed identity resource
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityName string = managedIdentity.name
