# Data Modeling and Partitioning in Cosmos DB

When designing for Azure Cosmos DB, the approach to data modeling differs significantly from traditional relational databases. The goal is to optimize for scalability, throughput, and predictable performance.

## 1. Denormalization and Embedding

In relational systems, data is typically normalized across multiple tables to reduce redundancy. In Cosmos DB, **denormalization** is preferred. Data that is read and updated together should be stored together in a single JSON document. 

### Implementation in the Healthcare Claims System
A healthcare claim naturally contains a header (provider, patient, status) and multiple line items (procedures, diagnoses, costs). Instead of creating separate containers for `ClaimHeaders` and `ClaimLineItems` (which would require expensive cross-partition joins to reconstruct), we embed the line items directly within the `Claim` document.

```json
{
  "id": "claim-123",
  "ProviderId": "PRV-100",
  "PatientId": "PAT-456",
  "LineItems": [
    { "ProcedureCode": "99213", "Amount": 150.00 },
    { "ProcedureCode": "36415", "Amount": 45.00 }
  ]
}
```
This ensures that retrieving a complete claim is a fast, single-item read operation that costs only 1 Request Unit (RU), minimizing latency.

## 2. Partitioning Strategy

Cosmos DB scales horizontally by distributing data across physical servers (physical partitions) based on a property you select called the **Partition Key**. Selecting the right partition key is the most crucial design decision.

### Criteria for a Good Partition Key
- **High Cardinality:** A large number of distinct values to ensure data is spread evenly.
- **Even Distribution:** Avoids "hot partitions" where one physical server receives the bulk of the read/write traffic.
- **Query Routing:** Ideally, most of your queries should include the partition key in the `WHERE` clause. This allows Cosmos DB to route the query to a single physical partition (an in-partition query), rather than broadcasting it to all partitions (a cross-partition query).

### Our Choice: Synthetic Partition Keys
For the Healthcare system, querying claims by `ProviderId` is the most common access pattern. However, using `ProviderId` alone as a partition key presents a risk: a massive hospital network might generate so much data that it exceeds the 20GB limit of a single logical partition, or causes a write bottleneck.

To solve this, we employ a **Synthetic Partition Key**: `ProviderId_YearMonth` (e.g., `PRV-100_202310`).
- **Scalability:** It breaks down a massive provider's data into monthly chunks, guaranteeing we never hit partition limits.
- **Efficiency:** When a provider views their recent dashboard, the query targets the current month (`WHERE c.PartitionKey = 'PRV-100_202310'`), resulting in a highly efficient, single-partition query.
