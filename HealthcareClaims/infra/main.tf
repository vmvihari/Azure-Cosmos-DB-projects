terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

locals {
  resource_prefix = "${var.project_name}-${var.environment}"
}

# 1. Resource Group
resource "azurerm_resource_group" "rg" {
  name     = "rg-${local.resource_prefix}"
  location = var.location
}

# 2. Cosmos DB Account (Multi-region)
resource "azurerm_cosmosdb_account" "cosmos_db" {
  name                = "cosmos-${local.resource_prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  # Enable Multi-region Writes
  enable_multiple_write_locations = true
  enable_automatic_failover       = true

  # Consistency Policy
  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = var.location
    failover_priority = 0
  }

  geo_location {
    location          = var.secondary_location
    failover_priority = 1
  }
}

# 3. Cosmos DB Database
resource "azurerm_cosmosdb_sql_database" "database" {
  name                = "HealthcareDB"
  resource_group_name = azurerm_resource_group.rg.name
  account_name        = azurerm_cosmosdb_account.cosmos_db.name
  throughput          = 400
}

# 4. Cosmos DB Container (Claims)
resource "azurerm_cosmosdb_sql_container" "claims_container" {
  name                  = "Claims"
  resource_group_name   = azurerm_resource_group.rg.name
  account_name          = azurerm_cosmosdb_account.cosmos_db.name
  database_name         = azurerm_cosmosdb_sql_database.database.name
  
  # Partition Key Strategy: ProviderId_YearMonth helps distribute massive volumes
  partition_key_path    = "/PartitionKey"
  partition_key_version = 2

  # Enable TTL (Time to live) - e.g., default off, but schema allows it
  default_ttl = -1 

  # Indexing Policy: Optimize for reads and RU costs
  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }

    # Exclude large text fields from indexing to save RUs
    excluded_path {
      path = "/MedicalNotes/?"
    }

    # Composite Index for dashboard queries
    composite_index {
      index {
        path  = "/Status"
        order = "ascending"
      }
      index {
        path  = "/SubmittedDate"
        order = "descending"
      }
    }
  }
}

# 5. Cosmos DB Container (Leases - Required for Change Feed)
resource "azurerm_cosmosdb_sql_container" "leases_container" {
  name                  = "leases"
  resource_group_name   = azurerm_resource_group.rg.name
  account_name          = azurerm_cosmosdb_account.cosmos_db.name
  database_name         = azurerm_cosmosdb_sql_database.database.name
  partition_key_path    = "/id"
  partition_key_version = 1
  throughput            = 400
}

# 6. SignalR Service (Serverless mode for Azure Functions)
resource "azurerm_signalr_service" "signalr" {
  name                = "sigr-${local.resource_prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  sku {
    name     = "Free_F1"
    capacity = 1
  }
  
  cors {
    allowed_origins = ["*"]
  }

  service_mode = "Serverless"
}

# 7. Application Insights
resource "azurerm_application_insights" "app_insights" {
  name                = "appi-${local.resource_prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
}

# 8. Storage Account (Required for Azure Functions)
resource "azurerm_storage_account" "storage" {
  name                     = "st${replace(local.resource_prefix, "-", "")}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

# 9. App Service Plan (Serverless/Consumption)
resource "azurerm_service_plan" "asp" {
  name                = "asp-${local.resource_prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Windows"
  sku_name            = "Y1"
}

# 10. Azure Function App (Backend)
resource "azurerm_windows_function_app" "function_app" {
  name                = "func-${local.resource_prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  service_plan_id     = azurerm_service_plan.asp.id

  storage_account_name       = azurerm_storage_account.storage.name
  storage_account_access_key = azurerm_storage_account.storage.primary_access_key

  site_config {
    application_insights_connection_string = azurerm_application_insights.app_insights.connection_string
    application_stack {
      dotnet_version = "v8.0"
      use_dotnet_isolated_runtime = true
    }
  }

  app_settings = {
    "CosmosDbConnectionString" = azurerm_cosmosdb_account.cosmos_db.connection_strings[0]
    "SignalRConnectionString"  = azurerm_signalr_service.signalr.primary_connection_string
  }
}
