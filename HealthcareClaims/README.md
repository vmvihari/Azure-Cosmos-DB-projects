# Real-Time Healthcare Claims Processing System

An enterprise-grade, real-time application demonstrating advanced Azure Cosmos DB architectural patterns, built with C# .NET 8, Angular 18+, and Terraform.

## 🚀 Architecture Overview

This project simulates a high-throughput healthcare claims ingestion system. When claims are submitted by providers, they are validated and saved into Cosmos DB. The **Cosmos DB Change Feed** is then utilized to instantly push these new or updated claims to a real-time Angular dashboard using **Azure SignalR Service**.

### Key Technologies
- **Database**: Azure Cosmos DB (NoSQL API)
- **Backend**: C# .NET 8 (Azure Functions Isolated Worker)
- **Frontend**: Angular 18+
- **Real-Time Messaging**: Azure SignalR Service
- **Infrastructure**: Terraform

> 📖 **Architecture Deep Dive:** For a full system diagram and explanation of how data flows between these components, please read the [**Project Architecture Document**](./docs/Project_Architecture.md).

---

## 📚 Architectural Concepts & Documentation

This repository contains a deep dive into the architectural decisions made for this system. The documentation is structured to comprehensively explain how advanced Cosmos DB concepts are practically applied in an enterprise scenario, which is highly useful for conceptual understanding and interview preparation.

Please review the following technical guides in the `/docs/Concepts` folder:
1. [Data Modeling & Partitioning](./docs/Concepts/01_Data_Modeling_and_Partitioning.md)
2. [Change Feed & Event-Driven Architecture](./docs/Concepts/02_Change_Feed_and_Event_Driven_Architecture.md)
3. [Global Distribution & Consistency Models](./docs/Concepts/03_Global_Distribution_and_Consistency.md)
4. [Indexing & Performance Optimization](./docs/Concepts/04_Indexing_and_Performance_Optimization.md)
5. [Server-Side Programming & Transactions (ACID)](./docs/Concepts/05_Server_Side_Programming_and_Transactions.md)

---

## 🛠️ Prerequisites

To run this project locally, ensure you have the following installed:
1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. [Node.js & npm](https://nodejs.org/) (for Angular)
3. [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (v4)
4. [Azure Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator)
5. *Optional*: [Terraform](https://developer.hashicorp.com/terraform/downloads) (if you intend to provision real Azure resources)

---

## 🏃‍♂️ How to Run Locally

### 1. Start the Cosmos DB Emulator
Ensure the Cosmos DB Emulator is running on your machine. The backend is configured by default to use the standard emulator connection string.

### 2. Start the Backend API (Azure Functions)
Navigate to the API directory and start the Functions host:
```bash
cd src/api/HealthcareClaims.Api
func start
```
*Note: The API runs on `http://localhost:7071` by default.*

### 3. Start the Angular Dashboard
Open a new terminal window, navigate to the frontend directory, install dependencies, and start the development server:
```bash
cd src/client/claims-dashboard
npm install
npm start
```
*Open your browser to `http://localhost:4200` to view the dashboard.*

### 4. Run the Claim Simulator
To see the real-time architecture in action, open a third terminal window and run the simulator to generate mock traffic:
```bash
cd src/simulator/ClaimGenerator
dotnet run
```

Watch the terminal as claims are generated, and switch to your browser (`http://localhost:4200`) to see the dashboard updating in real-time without refreshing the page!

---

## ☁️ Deploying to Azure

The `/infra` folder contains Terraform scripts to provision the necessary resources in Azure (Cosmos DB with multi-region writes, Function Apps, and SignalR).

```bash
cd infra
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```
*Note: You must be logged into the Azure CLI (`az login`) and have an active subscription to provision resources.*
