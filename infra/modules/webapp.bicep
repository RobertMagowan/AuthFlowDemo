targetScope = 'resourceGroup'

@description('Azure region in which the Web App will be deployed.')
param location string

@description('Name of the App Service Plan.')
param appServicePlanName string

@description('SKU used by the App Service Plan.')
param appServicePlanSkuName string = 'B1'

@description('Globally unique name of the Web App.')
param webAppName string

@description('Tags applied to the App Service resources.')
param tags object

module appServicePlan 'br/public:avm/res/web/serverfarm:0.7.0' = {
  name: 'deployAppServicePlan'
  params: {
    name: appServicePlanName
    location: location
    kind: 'linux'
    reserved: true
    skuName: appServicePlanSkuName
    skuCapacity: 1
    zoneRedundant: false
    tags: tags
  }
}
