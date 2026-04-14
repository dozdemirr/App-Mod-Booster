param suffix string
param managedIdentityPrincipalId string

var openAiName = toLower('aoai-appmodassist-${suffix}')
var searchName = toLower('srchappmod${substring(suffix, 0, 8)}')
var roleDefinitionOpenAiUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
var roleDefinitionSearchContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')

resource openAi 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAiName
  location: 'swedencentral'
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: 'Enabled'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAi
  name: 'gpt-4o'
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
  sku: {
    name: 'Standard'
    capacity: 8
  }
}

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: resourceGroup().location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

resource openAiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAi.id, managedIdentityPrincipalId, roleDefinitionOpenAiUser)
  scope: openAi
  properties: {
    roleDefinitionId: roleDefinitionOpenAiUser
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, managedIdentityPrincipalId, roleDefinitionSearchContributor)
  scope: searchService
  properties: {
    roleDefinitionId: roleDefinitionSearchContributor
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAi.properties.endpoint
output openAIModelName string = gpt4oDeployment.name
output openAIName string = openAi.name
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
