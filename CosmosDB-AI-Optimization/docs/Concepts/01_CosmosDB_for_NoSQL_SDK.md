# Cosmos DB for NoSQL using the .NET SDK

## Concept Overview
Azure Cosmos DB is a fully managed, globally distributed NoSQL database. For early-stage development and learning, you can provision a Serverless account to minimize costs.

In a NoSQL setup, data is stored in **Containers**, and each item within the container is formatted as a JSON document. While there is no rigid schema, it is crucial to pick an effective **Partition Key** (e.g., `/category`). The partition key determines how data is distributed across physical servers. 

When retrieving data using the SDK, there are several nuances to manage:
1. **Parameterized Queries**: Always avoid string interpolation for user-provided data. Use parameterized queries to prevent injection and allow the query engine to cache the execution plan.
2. **Cross-Partition Queries**: If your query lacks the partition key in its `WHERE` clause, Cosmos DB must search every physical partition (a "fan-out" query). By default, the SDK prevents this to protect you from unexpected high costs. You must explicitly allow it.
3. **Request Units (RUs)**: Cosmos DB bills based on RUs. The SDK returns the RU cost in the response headers of every query so you can measure the efficiency of your code.

---

## C# Implementation Instructions (Part 1)

In this series, you will incrementally build a single C# Console Application that implements all concepts. 

> [!IMPORTANT]
> **Enterprise Best Practices enforced in this section:**
> - Grouping resources in a dedicated Resource Group.
> - Securing credentials using Environment Variables.
> - Using a Singleton `CosmosClient` to avoid socket exhaustion.
> - Configuring `CosmosClientOptions` for Direct Connection mode and regional affinity.

### 1. Infrastructure & Account Setup
1. **Resource Group**: In the Azure Portal, create a new Resource Group (e.g., `rg-cosmos-ai-demo`) to logically group and manage the lifecycle of your database and AI resources.
2. **Database Account**: Create a Cosmos DB (NoSQL API) account inside your Resource Group. Choose the **Serverless** option.
3. **Container**: Create a Database named `AI-demo`, and a Container named `products` with the partition key `/category`.
4. Manually insert a few JSON items with `id`, `category`, `name`, and `price` fields using Data Explorer. Here are some sample JSON items you can copy and paste:

   **Sample 1:**
   ```json
   {
       "id": "prod-001",
       "category": "electronics",
       "name": "Wireless Noise-Canceling Headphones",
       "price": 299.99
   }
   ```
   **Sample 2:**
   ```json
   {
       "id": "prod-002",
       "category": "electronics",
       "name": "Mechanical Gaming Keyboard",
       "price": 129.99
   }
   ```
   **Sample 3:**
   ```json
   {
       "id": "prod-003",
       "category": "books",
       "name": "Design Patterns: Elements of Reusable Object-Oriented Software",
       "price": 45.00
   }
   ```

5. Copy your connection string from the **Keys** blade.

### 2. Scaffold the Application
Open a terminal in this project directory and run the following commands to create a new console app and install the required SDK:
```bash
dotnet new console -n CosmosAIApp
cd CosmosAIApp
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Newtonsoft.Json
```

### 3. Securely Store Credentials
> [!WARNING]
> Never hardcode connection strings in your source code.

Set your connection string as a local environment variable on your machine.
- **Windows (PowerShell)**: `$env:COSMOS_CONNECTION_STRING="AccountEndpoint=https://..."`
- **Linux/Mac**: `export COSMOS_CONNECTION_STRING="AccountEndpoint=https://..."`

### 4. Initialize the Singleton Client
Open `Program.cs` in your editor. Replace its contents with the following code. Notice how we configure `CosmosClientOptions` and instantiate the client only once (Singleton pattern).

```csharp
using Microsoft.Azure.Cosmos;

Console.WriteLine("Starting Cosmos DB AI App...");

string? connectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Please set the COSMOS_CONNECTION_STRING environment variable.");
    return;
}

// BEST PRACTICE: Configure options for Direct Mode and specify your primary region
CosmosClientOptions options = new CosmosClientOptions()
{
    ConnectionMode = ConnectionMode.Direct,
    ApplicationRegion = Regions.EastUS // Replace with your deployment region
};

// BEST PRACTICE: The CosmosClient should be a Singleton for the lifetime of your application.
using CosmosClient client = new CosmosClient(connectionString, options);

Database database = client.GetDatabase("AI-demo");
Container productsContainer = database.GetContainer("products");

await RunBasicQueryAsync(productsContainer);

static async Task RunBasicQueryAsync(Container container)
{
    Console.WriteLine("\n--- Running Basic Query ---");
    string sqlQueryText = "SELECT * FROM c WHERE c.category = @category";
    
    QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
        .WithParameter("@category", "electronics");

    // In the V3 SDK, cross-partition queries are enabled automatically.
    using FeedIterator<dynamic> queryResultSetIterator = container.GetItemQueryIterator<dynamic>(queryDefinition);

    while (queryResultSetIterator.HasMoreResults)
    {
        FeedResponse<dynamic> currentResultSet = await queryResultSetIterator.ReadNextAsync();
        foreach (var item in currentResultSet)
        {
            Console.WriteLine($"\tFound Item: {item.id} - {item.name}");
        }
        Console.WriteLine($"\tQuery RU Charge: {currentResultSet.RequestCharge}");
    }
}
```

### 5. Run the App
In your terminal, inside the `CosmosAIApp` directory, run:
```bash
dotnet run
```
You should see the output of the query and the Request Unit (RU) cost charged for retrieving the documents.
