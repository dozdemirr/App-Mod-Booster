targetScope = 'resourceGroup'

@description('Primary deployment location.')
param location string = resourceGroup().location

@description('Azure AD object ID for SQL Entra admin.')
param adminObjectId string

@description('Azure AD UPN for SQL Entra admin.')
param adminLogin string

@description('Managed identity name.')
param managedIdentityName string = 'mid-appmodassist-${uniqueString(resourceGroup().id)}'

@description('Deploy GenAI resources.')
param deployGenAi bool = false

var suffix = toLower(uniqueString(resourceGroup().id))

module appService 'modules/app-service.bicep' = {
  name: 'appservice-${suffix}'
  params: {
    location: location
    suffix: suffix
    managedIdentityName: toLower(managedIdentityName)
  }
}

module azureSql 'modules/azure-sql.bicep' = {
  name: 'azuresql-${suffix}'
  params: {
    location: location
    suffix: suffix
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityName: appService.outputs.managedIdentityName
  }
}

module genai 'modules/genai.bicep' = if (deployGenAi) {
  name: 'genai-${suffix}'
  params: {
    suffix: suffix
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

output appServiceName string = appService.outputs.appServiceName
output managedIdentityName string = appService.outputs.managedIdentityName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlDatabaseName string = azureSql.outputs.sqlDatabaseName
output openAIEndpoint string = genai.outputs.openAIEndpoint ?? ''
output openAIModelName string = genai.outputs.openAIModelName ?? ''
output openAIName string = genai.outputs.openAIName ?? ''
output searchEndpoint string = genai.outputs.searchEndpoint ?? ''
