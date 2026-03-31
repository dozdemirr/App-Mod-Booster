@description('Location for main resources')
param location string = 'uksouth'

@description('Entra ID admin object ID for SQL')
param adminObjectId string

@description('Entra ID admin login (UPN) for SQL')
param adminLogin string

@description('Deploy GenAI resources')
param deployGenAI bool = false

module appService 'app-service.bicep' = {
  name: 'appServiceDeploy'
  params: {
    location: location
  }
}

module sqlDatabase 'azure-sql.bicep' = {
  name: 'sqlDatabaseDeploy'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeploy'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output sqlServerFqdn string = sqlDatabase.outputs.sqlServerFqdn
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
