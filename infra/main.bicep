targetScope = 'subscription'

@description('Azure region in which the resources will be deployed.')
param location string

@description('Short name for the application.')
param workloadName string

@description('Deployment environment.')
@allowed([
  'development'
  'test'
  'production'
])
param environmentName string

@description('SKU used by the App Service Plan.')
param appServicePlanSkuName string

var resourceGroupName = 'rg-${workloadName}-${environmentName}-${location}'

var appServicePlanName = 'asp-${workloadName}-${environmentName}'

// Calculate this separately because the expression should not span
// multiple lines inside an interpolated string.
var webAppNameSuffix = uniqueString(
  subscription().subscriptionId,
  resourceGroupName
)

var webAppName = 'app-${workloadName}-${environmentName}-${webAppNameSuffix}'

var tags = {
  application: workloadName
  environment: environmentName
  managedBy: 'Bicep'
}

module resourceGroupModule 'br/public:avm/res/resources/resource-group:0.4.3' = {
  name: 'deployResourceGroup'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

module webAppResources './modules/webapp.bicep' = {
  name: 'deployWebAppResources'
  scope: resourceGroup(resourceGroupName)

  dependsOn: [
    resourceGroupModule
  ]

  params: {
    location: location
    appServicePlanName: appServicePlanName
    appServicePlanSkuName: appServicePlanSkuName
    webAppName: webAppName
    tags: tags
  }
}

output resourceGroupName string = resourceGroupName
output appServicePlanName string = appServicePlanName
output webAppName string = webAppResources.outputs.webAppName
output webAppUrl string = webAppResources.outputs.webAppUrl
