# Change Feed and Event-Driven Architecture

The Azure Cosmos DB Change Feed is a persistent, time-ordered log of inserts and updates that occur within a container. It acts as the backbone for building reactive, event-driven microservices.

## 1. How the Change Feed Works

Unlike polling a database for changes (which consumes RUs and compute resources), the Change Feed pushes events to subscribers. When a document is created or modified, the change is appended to the feed. 

*Note: The standard Change Feed tracks inserts and updates, but not hard deletes. To handle deletes, a "soft-delete" pattern (using a `ttl` or an `IsDeleted` boolean) is standard practice.*

## 2. Event-Driven Microservices

In modern enterprise architectures, the Change Feed enables decoupling systems. Multiple independent consumers can listen to the same feed at their own pace without impacting the performance of the core transactional database.

### Implementation in the Healthcare Claims System
We utilize the Change Feed to power the real-time aspects of the platform:

1. **Real-Time SignalR Dashboard:** We deployed an Azure Function (`ClaimChangeFeed`) triggered by Cosmos DB. When a claim status updates, the function reads the event and broadcasts it via Azure SignalR to the Angular frontend. This provides live updates to providers without their browsers needing to poll the API.
2. **Asynchronous Processing:** If we were to add a Fraud Detection engine, it could exist as a completely separate Azure Function listening to the *same* Change Feed. It would analyze new claims and flag anomalies without slowing down the initial claim ingestion API.

## 3. Scaling the Change Feed Processor

When using the Cosmos DB Trigger in Azure Functions, the underlying technology is the **Change Feed Processor library**. 

To maintain state and guarantee at-least-once delivery, the processor uses a separate Cosmos DB container called the **Leases Container**. This container stores "bookmarks" (continuation tokens) tracking exactly where the processor left off for each physical partition.

If the volume of claims spikes, Azure Functions automatically scales out. The Change Feed Processor intelligently distributes the leases across the active compute instances. If your container has 10 physical partitions, you can scale up to 10 parallel Function instances processing the feed simultaneously, ensuring massive throughput capabilities.
