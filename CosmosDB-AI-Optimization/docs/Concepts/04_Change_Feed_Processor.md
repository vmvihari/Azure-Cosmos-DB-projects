# Change Feed Processor

## Concept Overview

The **Cosmos DB Change Feed** provides a persistent, chronological log of all inserts and updates (note: it does not handle deletions) occurring within a container. 

This is the backbone of Event-Driven architectures in Cosmos DB. Instead of writing code that constantly polls the database asking "are there any new products?", you can use the **Change Feed Processor**. The processor listens to the change feed and pushes batches of new or updated items to a callback function in your code.

Because the change feed handles a distributed system, it requires a secondary container (called the `leases` container). The processor uses this `leases` container to store its state, manage load balancing across multiple worker instances, and keep a "bookmark" of the last item it processed. If your application crashes and restarts, it reads the lease container and resumes exactly where it left off, guaranteeing no missed events.

---

## C# Implementation Instructions (Part 4)

Finally, we will add a real-time event listener to our `CosmosAIApp`.

> [!TIP]
> **Enterprise Best Practices enforced in this section:**
> - **Horizontal Scaling**: The Change Feed Processor supports dynamic horizontal scaling. By assigning a unique `.WithInstanceName()` to each running copy of your application, Cosmos DB automatically balances the partition leases among them. If one instance crashes, the others pick up its leases!

### 1. Setup Leases Container
1. Open the Azure Portal Data Explorer.
2. In your `AI-demo` database, create a new container named `leases`.
3. Set the Partition Key to `/id`.

### 2. Add the Change Feed Method
Open `Program.cs`. Add the following local methods to the bottom of the file. They contain the callback method and the initialization logic.

```csharp
static async Task HandleChangesAsync(
            IReadOnlyCollection<dynamic> changes, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"\n[Change Feed] Change detected! Received {changes.Count} items.");
            foreach (var item in changes)
            {
                Console.WriteLine($"\tUpdated Item ID: {item.id}, Category: {item.category}");
            }
        }

        static async Task StartChangeFeedProcessorAsync(Container productsContainer, Container leasesContainer)
        {
            Console.WriteLine("\n--- Starting Change Feed Processor ---");
            
            // BEST PRACTICE: Use a unique instance name (like the machine hostname or pod ID).
            // This allows you to run multiple instances of this app to scale out processing.
            string instanceName = $"consoleHost-{Guid.NewGuid().ToString().Substring(0, 5)}";
            
            ChangeFeedProcessor processor = productsContainer
                .GetChangeFeedProcessorBuilder<dynamic>(
                    processorName: "productsProcessor", 
                    onChangesDelegate: HandleChangesAsync)
                .WithInstanceName(instanceName)
                .WithLeaseContainer(leasesContainer)
                .Build();

            await processor.StartAsync();
            Console.WriteLine($"Processor '{instanceName}' started. Leave this running and make changes in the Azure Portal...");
            
            // Keep the app running to listen for events
            Console.WriteLine("Press any key to stop.");
            Console.ReadKey();

            await processor.StopAsync();
        }
```

### 3. Start the Processor
Add the initialization logic to your top-level statements:

```csharp
// ... existing setup ...
Container leasesContainer = database.GetContainer("leases");

await StartChangeFeedProcessorAsync(productsContainer, leasesContainer);
```

### 4. Test the Real-Time Feed
1. Run the app: `dotnet run`. It will start the processor and pause, waiting for events.
2. Go to the Azure Portal Data Explorer and manually create a new item in the `products` container, or edit an existing item.
3. Look back at your terminal. Within a few seconds, the console app will detect the change and print the item details!
