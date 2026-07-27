# Indexing and Performance Optimization

Cosmos DB is a schema-agnostic database that automatically indexes every property of every document upon ingestion by default. While this provides incredible developer agility (allowing you to query any field without upfront schema design), it requires tuning for enterprise-scale workloads.

## 1. Tuning the Indexing Policy

Every indexed property consumes storage and adds a small amount of compute overhead (Request Units, or RUs) to every write operation. 

In a high-throughput system, you only want to index the fields you actually query. 

### Excluded Paths
In the Healthcare Claims system, a claim might contain a `MedicalNotes` field. This could be a massive JSON object or a long string of unstructured text from a doctor. We never execute queries like `WHERE c.MedicalNotes = 'xyz'`. 

By configuring an **Excluded Path** (`/MedicalNotes/?`) in our Terraform scripts, Cosmos DB stops indexing this field. This drastically reduces the RU cost of inserting a new claim and minimizes the storage footprint of the index, saving money.

## 2. Composite Indexes

By default, Cosmos DB creates single-property indexes. If a query filters or sorts by a single property, it runs efficiently. However, if your application frequently executes queries that involve an `ORDER BY` clause with multiple properties, a **Composite Index** is required.

### Implementation in the Dashboard
Our Angular dashboard frequently needs to query claims by status and sort them by submission date:
```sql
SELECT * FROM c WHERE c.Status = 'Pending' ORDER BY c.SubmittedDate DESC
```
Without a composite index, Cosmos DB cannot execute this query efficiently and may return an error asking you to create one. In our Terraform setup, we defined a composite index on `(Status ASC, SubmittedDate DESC)` to ensure this dashboard query executes with minimal RUs and low latency.

## 3. Managing Request Units (RUs)

RUs are the currency of Cosmos DB, abstracting CPU, Memory, and IOPS. A 1 KB point read (reading a document by its ID and Partition Key) costs exactly 1 RU.

If your application exceeds its provisioned RU limit, Cosmos DB returns an HTTP 429 (Too Many Requests) error. 
- **SDK Handling:** The .NET SDK automatically handles 429 errors by inspecting the `x-ms-retry-after-ms` header and retrying the request seamlessly.
- **Autoscale:** To handle unpredictable traffic (e.g., a massive influx of claims at the end of the month), Cosmos DB offers **Autoscale Throughput**. You set a maximum RU/s, and the database automatically and instantly scales between 10% of that max and the maximum based on the real-time workload, optimizing costs.
