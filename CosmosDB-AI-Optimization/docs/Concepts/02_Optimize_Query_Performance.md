# Optimize Query Performance

## Concept Overview

### 1. Partitioning Strategy (Best Practice)
Before optimizing indexes, you must optimize your partition key. A poor partition key leads to "hot partitions" (where one physical server gets overwhelmed while others are idle) or expensive cross-partition queries.
- **High Cardinality**: Choose a key with many distinct values (e.g., `/tenantId` or `/deviceId` instead of `/status`).
- **Uniform Distribution**: Ensure data volume and requests are evenly spread across the partition key values.

### 2. Indexing Policies
By default, Cosmos DB automatically indexes every property of every item (`/*`). While this guarantees that any query will run efficiently using the index, it also means that every insert and update consumes extra Request Units (RUs) to maintain those indexes.

If your application has fields that are never used in a `WHERE` clause, `ORDER BY`, or `JOIN` (for example, a large description text, or a constantly updating `price`), you can **exclude** those paths from the indexing policy. Doing so reduces the RU cost of write operations, saving you money on write-heavy workloads. The trade-off is that if you do run a query filtering on that excluded field, Cosmos DB will perform an expensive full table scan.

### 3. Handling 429 Rate Limiting (Best Practice)
If you exceed your provisioned RUs, Cosmos DB returns a `429 Too Many Requests` error. The .NET SDK automatically handles retries for these transient errors, but you should explicitly configure `MaxRetryAttemptsOnRateLimitedRequests` and `MaxRetryWaitTimeOnRateLimitedRequests` in your `CosmosClientOptions` for heavy bulk operations.

### 4. Consistency Levels
In a globally distributed database, consistency models define the trade-offs between performance, availability, and data correctness (staleness). Cosmos DB provides five levels:
- **Strong**: Highest consistency. A write is not committed until all regions have synchronized. Reads always return the most recent write. Highest latency, lowest availability.
- **Bounded Staleness**: Strong consistency, but lagging by a defined threshold (e.g., maximum 5 seconds or 100 versions).
- **Session**: The default. It guarantees consistency *within a specific user's session*. You will always read your own writes in order. It's the sweet spot for most web applications.
- **Consistent Prefix**: Guarantees order. You'll never see writes out of order, but there's no guarantee on how far behind the data might be.
- **Eventual**: Lowest consistency, highest performance. Reads might be completely out of order, but replicas will eventually converge. Best for non-critical logs or append-only architectures.

---

## C# Implementation Instructions (Part 2)

We will now add a new method to our `CosmosAIApp` to test how indexing policies affect insert performance.

> [!IMPORTANT]
> **Enterprise Best Practices enforced in this section:**
> - Bulk operations with optimized configurations.
> - Handling rate limiting natively via the SDK.

### 1. Update CosmosClientOptions
Open `Program.cs`. Update your `CosmosClientOptions` to handle bulk execution and explicitly define rate-limiting retry policies:

```csharp
CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationRegion = Regions.EastUS,
                AllowBulkExecution = true, // BEST PRACTICE: Optimizes throughput for bulk inserts
                MaxRetryAttemptsOnRateLimitedRequests = 9,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            };
```

### 2. Add the Bulk Insert Method
Add the following local method to the bottom of `Program.cs`:

```csharp
static async Task RunBulkInsertExperimentAsync(Container container)
        {
            Console.WriteLine("\n--- Running Bulk Insert Experiment ---");
            double totalRUs = 0;
            
            // Generate a list of tasks for bulk execution
            var tasks = new System.Collections.Generic.List<Task<ItemResponse<dynamic>>>();
            
            for (int i = 0; i < 50; i++) // Using 50 for quick testing
            {
                var newProduct = new 
                { 
                    id = Guid.NewGuid().ToString(), 
                    category = "electronics", 
                    name = $"Test Product {i}", 
                    price = new Random().Next(10, 1000) 
                };
                
                tasks.Add(container.CreateItemAsync<dynamic>(newProduct));
            }
            
            // Execute all tasks in parallel
            var responses = await Task.WhenAll(tasks);
            foreach(var response in responses)
            {
                totalRUs += response.RequestCharge;
            }
            
            Console.WriteLine($"Total items inserted: 50");
            Console.WriteLine($"Average RU per insert: {totalRUs / 50}");
        }
```

### 3. Update execution and Run Baseline
Add the following call to your top-level statements: `await RunBulkInsertExperimentAsync(productsContainer);`.
Run the app in your terminal (`dotnet run`) and note the "Average RU per insert".

### 4. Modify Indexing Policy
1. Go to the Azure Portal -> Data Explorer -> `products` container -> **Settings** -> **Indexing Policy**. 
2. In the `excludedPaths` array, add a new entry for the price:
```json
    "excludedPaths": [
        { "path": "/\"_etag\"/?" },
        { "path": "/price/?" }
    ]
```
3. Save the policy. Wait a few moments for the policy to update.

### 5. Run the Optimized Experiment
Run the app again using `dotnet run`. Compare the average RU cost. You should observe a measurable decrease in the RU consumption per insert because Cosmos DB no longer has to compute and store an index for the `price` field!
