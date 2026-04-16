// genai.bicep
// Deploys Azure OpenAI + AI Search for GenAI Chat UI
// GPT-4o model in swedencentral (always, regardless of resource group location)
// Uses the managed identity from app-service.bicep for role assignments

param location string = 'swedencentral' // Always swedencentral for GPT-4o quota
param managedIdentityPrincipalId string
param managedIdentityId string

// Force lowercase for OpenAI custom subdomain requirement
var aoaiName = toLower('aoai-expensemgmt-${uniqueString(resourceGroup().id)}')
var searchName = toLower('search-expensemgmt-${uniqueString(resourceGroup().id)}')

// Azure OpenAI - S0 SKU
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: aoaiName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    customSubDomainName: aoaiName
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o model deployment - capacity 8
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
  }
}

// AI Search - S0 SKU (lowest cost)
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    publicNetworkAccess: 'Enabled'
  }
}

// Role: "Cognitive Services OpenAI User" for managed identity on OpenAI resource
// Role definition ID: 5e0bd9bd-7b93-4f28-af87-19fc36ad61bd
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, 'CognitiveServicesOpenAIUser')
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role: "Search Index Data Contributor" for managed identity on AI Search
// Role definition ID: 8ebe5a00-799e-43f5-93ac-243d3dce84a7
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, 'SearchIndexDataContributor')
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = 'gpt-4o'
output openAIName string = openAI.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
output searchName string = aiSearch.name
