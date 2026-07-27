# Server-Side Programming and Transactions (ACID)

Cosmos DB supports robust server-side programming using JavaScript. You can write Stored Procedures, Triggers, and User-Defined Functions (UDFs) that execute directly within the database engine, physically close to the data.

## 1. ACID Transactions

Cosmos DB provides full ACID (Atomicity, Consistency, Isolation, Durability) guarantees, but with one critical architectural caveat: **Transactions are strictly scoped to a single logical partition**. 

You cannot execute a transaction that spans multiple partition keys. This design choice ensures that Cosmos DB can scale horizontally without suffering the massive performance penalties associated with distributed two-phase commit protocols.

## 2. Stored Procedures

Stored Procedures in Cosmos DB are written in JavaScript and allow you to execute complex, multi-document logic within a single ACID transaction.

### Implementation Scenario: Bulk Claim Approval
Imagine a medical provider wants to approve a batch of 50 pending claims simultaneously. 

If this logic was implemented in the C# backend:
1. The backend reads 50 claims.
2. The backend updates the status of each.
3. The backend sends 50 separate write requests back to Cosmos DB.

If the backend crashes halfway through, you are left with partial updates (a lack of Atomicity). 

Instead, we can pass the array of 50 Claim IDs to a Cosmos DB Stored Procedure. The Stored Procedure runs on the server, scoped to that specific provider's logical partition (`ProviderId_YearMonth`). It retrieves the documents, updates them, and commits them. If an error occurs on the 49th document, the entire transaction is automatically rolled back, guaranteeing absolute data integrity.

## 3. Triggers

Cosmos DB supports two types of triggers. Unlike relational databases, these do not execute automatically; you must explicitly request them to run when performing an operation via the SDK (e.g., passing `PreTriggerInclude` in your request options).

- **Pre-Triggers:** Run *before* an operation is committed. Used primarily for strict data validation (e.g., ensuring a claim's total amount exactly matches the sum of its line items before allowing it to be saved) or for automatically appending data (like a timestamp).
- **Post-Triggers:** Run *after* an operation is committed. Used primarily for updating aggregates within the same partition. (e.g., When a new claim is inserted, a post-trigger could increment a "TotalClaimsSubmitted" counter document residing in that exact same partition).

## 4. Time-to-Live (TTL)

Cosmos DB features a native Time-to-Live capability that automatically deletes documents after a specified period, without consuming any of your provisioned RUs.

In the Healthcare system, when a provider is compiling a claim but hasn't submitted it yet, we save it as a "Draft". We can set a `ttl` property on the document (e.g., 604800 seconds, or 7 days). If they don't submit it within 7 days, a background thread in Cosmos DB purges the draft automatically, keeping storage costs low and the database clean.
