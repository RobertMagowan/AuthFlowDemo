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
