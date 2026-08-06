# Global Distribution and Consistency Models

Azure Cosmos DB is designed as a globally distributed database system. It allows you to replicate your data across any number of Azure regions worldwide with a single click, bringing data closer to your users and providing high availability.

## 1. Multi-Region Writes

By default, Cosmos DB operates with a single write region and multiple read regions. However, for enterprise systems that demand the lowest possible latency globally, **Multi-Region Writes** can be enabled (as configured in our Terraform scripts).

With Multi-Region Writes, an application (like a hospital client in New York or a clinic in California) always writes to its local Azure region. This guarantees write latencies of `<10ms` globally and provides a 99.999% SLA for both read and write availability.

### Conflict Resolution
When writing to multiple regions concurrently, there is a possibility that two clients update the exact same document simultaneously. Cosmos DB provides Conflict Resolution Policies to handle this gracefully:
- **Last Write Wins (LWW):** The default policy. It uses the system-generated `_ts` (timestamp) property to determine the winner.
- **Custom Resolution:** You can register a JavaScript Stored Procedure that executes automatically when a conflict is detected, allowing you to merge data or implement complex business logic to resolve the conflict (e.g., a "Rejected" status always overrides a "Pending" status).

## 2. Consistency Levels

Cosmos DB allows developers to make precise trade-offs between read consistency, availability, latency, and throughput by offering five distinct consistency levels.

1. **Strong:** Guarantees that a read always returns the most recent committed version. Offers the highest consistency but the lowest availability and highest latency. (Cross-region strong consistency is highly expensive).
2. **Bounded Staleness:** Reads are guaranteed to honor the sequence of writes, but may lag behind by a configured time interval or number of operations. Ideal for global BI dashboards.
3. **Session (The Default):** Provides "read your own writes" guarantees within a specific session. This is the most popular level as it perfectly balances performance and consistency for user-facing apps.
4. **Consistent Prefix:** Guarantees that reads never see out-of-order writes.
5. **Eventual:** No ordering guarantees. The replica will eventually converge. Offers the highest availability and lowest latency.

### Implementation in the Healthcare Claims System
We utilized **Session Consistency**. When a medical provider submits a claim, they expect to immediately see that claim on their dashboard. Session consistency ensures that the provider's specific session instantly reflects their writes, while allowing the global system to replicate asynchronously, saving Request Units (RUs) and maintaining sub-millisecond read latencies.
