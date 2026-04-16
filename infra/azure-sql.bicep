// azure-sql.bicep
// Deploys Azure SQL Server + Northwind Database
// Azure AD-Only Authentication (MCAPS SFI-ID4.2.2 policy)
// Stable API version: 2021-11-01

param location string = 'swedencentral'
param sqlServerName string = toLower('sql-expensemgmt-${uniqueString(resourceGroup().id)}')
param databaseName string = 'Northwind'

// Entra ID administrator details - set by the person deploying
param adminObjectId string
param adminLogin string

// Managed Identity to grant access
param managedIdentityPrincipalId string
param managedIdentityName string

// SQL Server - Azure AD-Only Authentication
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
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Northwind Database - Basic tier for development
resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Firewall rule: Allow Azure services (0.0.0.0 - 0.0.0.0)
resource firewallRuleAzureServices 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = database.name
