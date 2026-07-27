variable "project_name" {
  description = "The base name for all resources"
  type        = string
  default     = "healthcareclaims"
}

variable "location" {
  description = "The primary Azure region to deploy to"
  type        = string
  default     = "East US"
}

variable "secondary_location" {
  description = "The secondary Azure region for Cosmos DB global distribution"
  type        = string
  default     = "West US"
}

variable "environment" {
  description = "The environment name (e.g., dev, prod)"
  type        = string
  default     = "dev"
}
