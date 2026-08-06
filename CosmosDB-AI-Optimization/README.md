# Cosmos DB AI & Optimization

Welcome to the **Cosmos DB AI & Optimization** project! This project focuses on demonstrating advanced concepts in Azure Cosmos DB, emphasizing performance tuning, consistency models, AI vector similarity searches, and real-time processing via the Change Feed.

As per the requirements, this project is designed for you to follow along and implement the solutions yourself using **C#**.

## 🚀 Project Overview

The objective is to implement a robust, AI-ready product catalog using Cosmos DB. You will construct C# applications that interact with the Cosmos DB SDK to explore:
1. **Managing Cosmos DB & Optimizing Queries**: Exploring request unit (RU) costs, indexing policies, and consistency levels.
2. **Semantic Retrieval**: Creating vector embeddings of text using Azure OpenAI and storing them in Cosmos DB for vector similarity search.
3. **Change Feed Processing**: Setting up an asynchronous processor to detect and respond to item updates in real-time.

## 📚 Concepts & Implementation Guide

All instructions and conceptual knowledge needed to implement this project are broken down by topic. Read through these guides to understand the architecture and write the C# implementation:

1. [Cosmos DB for NoSQL SDK](./docs/Concepts/01_CosmosDB_for_NoSQL_SDK.md)
2. [Optimize Query Performance](./docs/Concepts/02_Optimize_Query_Performance.md)
3. [Semantic Retrieval & Vector Search](./docs/Concepts/03_Semantic_Retrieval.md)
4. [Change Feed Processor](./docs/Concepts/04_Change_Feed_Processor.md)

## 🛠️ Prerequisites

To build the project yourself, ensure you have the following installed:
1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. An **Azure Cosmos DB** Account (NoSQL API, Serverless tier recommended for demos).
3. An **Azure OpenAI** resource (with the `text-embedding-ada-002` model deployed).
