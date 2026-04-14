@description('Azure region for all resources')
param location string = 'swedencentral'

@description('Azure AD login name for the SQL administrator')
param adminLogin string

@description('Object ID of the Azure AD SQL administrator')
param adminObjectId string

@description('Deploy Azure OpenAI and AI Search resources')
param deployGenAI bool = false

module appServiceModule './app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
  }
}

module sqlModule './azure-sql.bicep' = {
  name: 'sqlDeployment'
  params: {
    location: location
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appServiceModule.outputs.managedIdentityPrincipalId
    managedIdentityName: 'mid-AppModAssist-14-16-40'
  }
}

module genAIModule './genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    location: location
    managedIdentityPrincipalId: appServiceModule.outputs.managedIdentityPrincipalId
    managedIdentityId: appServiceModule.outputs.managedIdentityId
  }
}

output appServiceName string = appServiceModule.outputs.appServiceName
output appServiceUrl string = appServiceModule.outputs.appServiceUrl
output sqlServerFqdn string = sqlModule.outputs.sqlServerFqdn
output sqlServerName string = sqlModule.outputs.sqlServerName
output databaseName string = sqlModule.outputs.databaseName
output managedIdentityClientId string = appServiceModule.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appServiceModule.outputs.managedIdentityPrincipalId
output openAIEndpoint string = deployGenAI ? genAIModule.?outputs.openAIEndpoint ?? '' : ''
output openAIModelName string = deployGenAI ? genAIModule.?outputs.openAIModelName ?? '' : ''
output searchEndpoint string = deployGenAI ? genAIModule.?outputs.searchEndpoint ?? '' : ''
