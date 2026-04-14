@description('Azure region for all resources')
param location string = 'swedencentral'

@description('Azure AD login name for the SQL administrator')
param adminLogin string

@description('Object ID of the Azure AD administrator')
param adminObjectId string

@description('Principal ID of the managed identity for SQL access')
param managedIdentityPrincipalId string

@description('Name of the managed identity')
param managedIdentityName string

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = toLower('sql-expensemgmt-${uniqueSuffix}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlServerFirewallAzureServices 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  name: databaseName
  parent: sqlServer
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = sqlDatabase.name
