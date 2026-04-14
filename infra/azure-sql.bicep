param location string = resourceGroup().location
param sqlServerNamePrefix string
param databaseName string = 'northwind'
param adminObjectId string
param adminLogin string
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: toLower('${sqlServerNamePrefix}${uniqueSuffix}')
  location: location
  properties: {
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

resource aadAdmin 'Microsoft.Sql/servers/administrators@2021-11-01' = {
  parent: sqlServer
  name: 'activeDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: adminLogin
    sid: adminObjectId
    tenantId: tenant().tenantId
    azureADOnlyAuthentication: true
  }
}

resource aadOnlyAuthentication 'Microsoft.Sql/servers/azureADOnlyAuthentications@2021-11-01' = {
  parent: sqlServer
  name: 'default'
  properties: {
    azureADOnlyAuthentication: true
  }
}

resource azureServicesFirewallRule 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'allowallazureips'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: toLower(databaseName)
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource dbManagerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sqlServer.id, managedIdentityPrincipalId, 'ms-databasemanager')
  scope: sqlServer
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec')
    principalType: 'ServicePrincipal'
    description: 'Grant managed identity permission to manage SQL databases.'
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
