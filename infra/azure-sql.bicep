@description('Location for resources')
param location string = 'uksouth'

@description('Entra ID admin object ID')
param adminObjectId string

@description('Entra ID admin login (UPN)')
param adminLogin string

@description('Managed Identity Principal ID for SQL access')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = 'sql-${uniqueSuffix}'
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      principalType: 'User'
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
  }
}

resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource firewallAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = databaseName
