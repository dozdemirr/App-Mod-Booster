param managedIdentityPrincipalId string
param location string = 'swedencentral'
param workloadName string = 'appmodassist'

var uniqueSuffix = toLower(uniqueString(resourceGroup().id))
var openAiName = toLower('aoai-${workloadName}-${uniqueSuffix}')
var openAiDeploymentName = 'gpt-4o'
var searchName = toLower('aisearch-${workloadName}-${uniqueSuffix}')
var cognitiveServicesOpenAiUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
var searchIndexDataReaderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '1407120a-92aa-4202-b7e9-c0e197c71c8f')

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }
}

resource openAiModel 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: openAiDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
    raiPolicyName: 'Microsoft.Default'
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    publicNetworkAccess: 'enabled'
    hostingMode: 'default'
    disableLocalAuth: true
  }
}

resource openAiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAi.id, managedIdentityPrincipalId, cognitiveServicesOpenAiUserRoleId)
  scope: openAi
  properties: {
    roleDefinitionId: cognitiveServicesOpenAiUserRoleId
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataReaderRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: searchIndexDataReaderRoleId
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAi.properties.endpoint
output openAIModelName string = openAiDeploymentName
output openAIName string = openAi.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
