targetScope = 'subscription'

@description('Azure region in which the resources will be deployed.')
param location string = 'uksouth'

@description('Short name for the application.')
param workloadName string = 'mywebapp'

@description('Deployment environment.')
@allowed([
  'development'
  'test'
  'prod'
])
param environmentName string = 'development'

var resourceGroupName = 'rg-${workloadName}-${environmentName}-${location}'

var tags = {
  application: workloadName
  environment: environmentName
  managedBy: 'Bicep'
}

module resourceGroup 'br/public:avm/res/resources/resource-group:0.4.3' = {
  name: 'deployResourceGroup'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

output resourceGroupName string = resourceGroupName
