param location string = resourceGroup().location
param appServicePlanName string
param appServiceName string
param managedIdentityName string

var uniqueSuffix = uniqueString(resourceGroup().id)

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: toLower('${appServicePlanName}-${uniqueSuffix}')
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: toLower(managedIdentityName)
  location: location
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: toLower('${appServiceName}-${uniqueSuffix}')
  location: location
  kind: 'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
  }
}

output appServiceName string = webApp.name
output managedIdentityResourceId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
