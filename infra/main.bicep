param location string = 'swedencentral'
param managedIdentityName string
param appServicePlanName string = 'asp-appmodassist'
param appServiceName string = 'app-appmodassist'
param sqlServerNamePrefix string = 'sqlappmodassist'
param databaseName string = 'northwind'
param adminObjectId string
param adminLogin string
param deployGenAi bool = false

module appService './app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    appServiceName: appServiceName
    managedIdentityName: managedIdentityName
  }
}

module sql './azure-sql.bicep' = {
  name: 'sqlDeployment'
  params: {
    location: location
    sqlServerNamePrefix: sqlServerNamePrefix
    databaseName: databaseName
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

module genai './genai.bicep' = if (deployGenAi) {
  name: 'genAiDeployment'
  params: {
    location: 'swedencentral'
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

output appServiceName string = appService.outputs.appServiceName
output managedIdentityResourceId string = appService.outputs.managedIdentityResourceId
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output sqlServerName string = sql.outputs.sqlServerName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output databaseName string = sql.outputs.databaseName
output openAIEndpoint string = genai.outputs?.openAIEndpoint ?? ''
output openAIModelName string = genai.outputs?.openAIModelName ?? ''
output openAIName string = genai.outputs?.openAIName ?? ''
output searchEndpoint string = genai.outputs?.searchEndpoint ?? ''
