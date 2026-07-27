# Healthcare Claims System: Architecture Overview

This document details the end-to-end architecture of the Real-Time Healthcare Claims Processing System, explaining how data flows from ingestion to the real-time frontend dashboard.

## System Architecture Diagram

```mermaid
graph TD
    %% Define components
    Sim[Claim Simulator\n(C# Console App)]
    API_Submit[SubmitClaim API\n(Azure Function - HTTP)]
    Cosmos[(Azure Cosmos DB\n'Claims' Container)]
    API_CF[ClaimChangeFeed API\n(Azure Function - Cosmos Trigger)]
    SignalR((Azure SignalR Service))
    UI[Angular Dashboard\n(Real-Time UI)]

    %% Data flow
    Sim -- "1. POST /api/claims" --> API_Submit
    API_Submit -- "2. Ingests Claim\n(Output Binding)" --> Cosmos
    Cosmos -- "3. Triggers Change Feed" --> API_CF
    API_CF -- "4. Broadcasts Update\n(Output Binding)" --> SignalR
    SignalR -- "5. Pushes 'claimUpdated' event" --> UI

    %% Styling
    classDef azure fill:#0072C6,stroke:#fff,stroke-width:2px,color:#fff;
    classDef client fill:#DD0031,stroke:#fff,stroke-width:2px,color:#fff;
    classDef simulator fill:#512BD4,stroke:#fff,stroke-width:2px,color:#fff;
    
    class API_Submit,Cosmos,API_CF,SignalR azure;
    class UI client;
    class Sim simulator;
```

## Component Breakdown

### 1. Claim Simulator (`src/simulator/ClaimGenerator`)
Since this is a backend-heavy architectural demonstration, we need a way to simulate real-world enterprise load. The simulator is a standalone C# Console application that rapidly generates randomized healthcare claims (using mock Provider IDs and Patient IDs) and posts them to our ingestion API. 

### 2. Ingestion API (`SubmitClaim` Azure Function)
This is a serverless HTTP endpoint written in C# (.NET 8 Isolated Worker). It serves as the front door for the system.
- **Responsibility**: Validates incoming claim payloads, computes the synthetic partition key (`ProviderId_YearMonth`), and inserts the document into Cosmos DB.
- **Optimization**: It utilizes Azure Functions Cosmos DB *Output Bindings*, meaning we don't have to write boilerplate Cosmos DB SDK code to perform the insert; the framework handles the connection pooling and execution efficiently.

### 3. Azure Cosmos DB
The core of the architecture. It provides high availability, global distribution, and sub-millisecond read/write latencies.
- **Data Model**: Denormalized. Claim headers and line items are stored in a single document.
- **Partitioning**: Uses a synthetic key to distribute load evenly and prevent hitting the 20GB logical partition limit.
- **Features Used**: Custom Indexing (excluding large medical notes), TTL (to auto-purge stale drafts), and Multi-Region Writes (configured via Terraform).

### 4. Change Feed Processor (`ClaimChangeFeed` Azure Function)
This is where the real-time, event-driven magic happens. This Azure Function does not expose an HTTP endpoint. Instead, it is triggered *automatically* by Cosmos DB.
- **Responsibility**: It listens to the Cosmos DB Change Feed. Whenever a claim is inserted (by the `SubmitClaim` API) or updated, Cosmos DB pushes the document to this function.
- **Action**: The function reads the updated claim and immediately pushes it to Azure SignalR Service using another Output Binding. 

### 5. Azure SignalR Service
A fully managed real-time messaging service. It acts as the bridge between the backend Azure Functions and the frontend Angular application. It maintains persistent WebSocket connections with thousands of connected web clients.

### 6. Real-Time Dashboard (Angular)
The client-facing application used by Medical Providers to track their claims.
- **Responsibility**: It establishes a persistent WebSocket connection to SignalR upon loading. 
- **Action**: Whenever SignalR broadcasts a `claimUpdated` event, the Angular component catches the event and dynamically updates the UI in real-time, providing a live, reactive experience without ever needing to query the database or poll the API.
