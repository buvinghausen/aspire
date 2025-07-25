﻿@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param storagesku string

resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: take('storage${uniqueString(resourceGroup().id)}', 24)
  kind: 'StorageV2'
  location: location
  sku: {
    name: storagesku
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
  tags: {
    'aspire-resource-name': 'storage'
  }
}

resource blob 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  name: 'default'
  parent: storage
}

resource queue 'Microsoft.Storage/storageAccounts/queueServices@2024-01-01' = {
  name: 'default'
  parent: storage
}

resource table 'Microsoft.Storage/storageAccounts/tableServices@2024-01-01' = {
  parent: storage
}

output blobEndpoint string = storage.properties.primaryEndpoints.blob

output queueEndpoint string = storage.properties.primaryEndpoints.queue

output tableEndpoint string = storage.properties.primaryEndpoints.table

output name string = storage.name
