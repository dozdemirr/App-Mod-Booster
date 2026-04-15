param location string = resourceGroup().location
param workloadName string = 'appmodassist'
param adminObjectId string
param adminLogin string
param deployGenAI bool = false

module appService './app-service.bicep' = {
  name: 'app-service-module'
  params: {
    location: location
    workloadName: workloadName
  }
}

module sql './azure-sql.bicep' = {
  name: 'azure-sql-module'
  params: {
    location: location
    workloadName: workloadName
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityName: appService.outputs.managedIdentityName
  }
}

module genai './genai.bicep' = if (deployGenAI) {
  name: 'genai-module'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    workloadName: workloadName
  }
}

output appName string = appService.outputs.appName
output appUrl string = 'https://${appService.outputs.appServiceDefaultHostName}'
output managedIdentityName string = appService.outputs.managedIdentityName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output openAIEndpoint string = genai?.outputs.openAIEndpoint ?? ''
output openAIModelName string = genai?.outputs.openAIModelName ?? ''
output openAIName string = genai?.outputs.openAIName ?? ''
output searchEndpoint string = genai?.outputs.searchEndpoint ?? ''
