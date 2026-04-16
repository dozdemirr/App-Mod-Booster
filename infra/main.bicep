// main.bicep
// Main orchestration template - deploys all resources
// Uses uniqueString(resourceGroup().id) for deterministic naming (no utcNow)

param location string = 'swedencentral'

// SQL Admin (Entra ID) - the person deploying
param adminObjectId string
param adminLogin string

// Optional: deploy GenAI resources
param deployGenAI bool = false

// Managed Identity name (day-hour-minute of creation)
param managedIdentityName string = 'mid-AppModAssist-16-10-15'

// App Service + Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeploy'
  params: {
    location: location
    managedIdentityName: managedIdentityName
  }
}

// Azure SQL
module sql 'azure-sql.bicep' = {
  name: 'sqlDeploy'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    managedIdentityName: appService.outputs.managedIdentityName
  }
}

// GenAI Resources (optional)
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeploy'
  params: {
    location: 'swedencentral'
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    managedIdentityId: appService.outputs.managedIdentityId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output appUrl string = '${appService.outputs.appServiceUrl}/Index'
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityName string = appService.outputs.managedIdentityName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlServerName string = sql.outputs.sqlServerName
output databaseName string = sql.outputs.databaseName

// GenAI outputs (null-safe for when deployGenAI=false)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
