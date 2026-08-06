# Semantic Retrieval and Vector Search

## Concept Overview

Traditional keyword search struggles when a user asks a question using different words than the ones stored in the database. **Semantic Search** solves this by converting text into numerical arrays called **Vectors** (or Embeddings). These vectors plot the semantic meaning of the text in a multi-dimensional space.

To perform a semantic search:
1. You pass your documents through a Large Language Model (like Azure OpenAI's `text-embedding-ada-002`) to generate an embedding (a vector of 1536 dimensions).
2. You store this vector alongside the document in Cosmos DB.
3. When a user submits a search query, you generate an embedding for their query using the exact same model.
4. You ask Cosmos DB to calculate the distance between the query vector and all the document vectors. The most mathematically similar vectors represent the most semantically relevant documents.

Cosmos DB supports native vector indexing and functions like `VectorDistance()` to perform these similarity searches extremely fast.

---

## C# Implementation Instructions (Part 3)

We will integrate vector search into our existing `CosmosAIApp`.

> [!IMPORTANT]
> **Enterprise Best Practices enforced in this section:**
> - **Resource Colocation**: AI services are latency-sensitive. Always deploy your Azure OpenAI resource in the *same Azure Region* and *same Resource Group* as your Cosmos DB account.
> - **Security**: Store OpenAI keys in environment variables, just like the Cosmos DB connection string.

### 1. Setup Cosmos DB and Azure OpenAI
1. **Enable Vector Search**: In the Cosmos DB Portal, navigate to **Settings -> Features** and enable the **Vector Search** feature.
2. **Create Container**: Create a new container in the `AI-demo` database called `documents`. Set the partition key to `/id`.
3. **Configure Vector Policy**: While creating the container, add the Vector Indexing Policy:
   - **Path**: `/embedding`
   - **Data Type**: `float32`
   - **Distance Function**: `cosine`
   - **Dimensions**: `1536`
4. **Deploy OpenAI Model**: In Azure OpenAI Studio, deploy the `text-embedding-ada-002` model. Ensure it is in the same region as your Cosmos DB.

### 2. Install OpenAI SDK & Set Environment Variables
In your terminal, install the OpenAI package:
```bash
dotnet add package Azure.AI.OpenAI --prerelease
```
Set your environment variables (using PowerShell syntax as an example):
```powershell
$env:OPENAI_ENDPOINT="https://YOUR_ACCOUNT.openai.azure.com/"
$env:OPENAI_KEY="YOUR_API_KEY"
```

### 3. Add the Semantic Search Method
Open `Program.cs`. Add the appropriate `using` statements at the top:
```csharp
using Azure;
using Azure.AI.OpenAI;
using System.Linq;
using System.Collections.Generic;
```

Add the new local method to the bottom of `Program.cs`:
```csharp
static async Task RunSemanticSearchAsync(Database database)
        {
            Console.WriteLine("\n--- Running Semantic Search ---");
            Container docsContainer = database.GetContainer("documents");
            
            // BEST PRACTICE: Secure credentials
            string openAiEndpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            string openAiKey = Environment.GetEnvironmentVariable("OPENAI_KEY");
            
            if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(openAiKey))
            {
                Console.WriteLine("Please set the OPENAI_ENDPOINT and OPENAI_KEY environment variables.");
                return;
            }

            OpenAIClient aiClient = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));

            // 1. Generate embedding for a test document and save it
            string documentText = "Azure Container Apps is a serverless platform for microservices.";
            var embedOptions = new EmbeddingsOptions("text-embedding-ada-002", new[] { documentText });
            Response<Embeddings> embedResponse = await aiClient.GetEmbeddingsAsync(embedOptions);
            float[] docVector = embedResponse.Value.Data[0].Embedding.ToArray();

            var newDoc = new { id = Guid.NewGuid().ToString(), text = documentText, embedding = docVector };
            await docsContainer.CreateItemAsync<dynamic>(newDoc);
            Console.WriteLine("Inserted document with vector embedding.");

            // 2. Generate embedding for a search query
            string queryText = "What runs containers without managing servers?";
            var queryOptions = new EmbeddingsOptions("text-embedding-ada-002", new[] { queryText });
            Response<Embeddings> queryResponse = await aiClient.GetEmbeddingsAsync(queryOptions);
            float[] queryVector = queryResponse.Value.Data[0].Embedding.ToArray();

            // 3. Execute Vector Search
            string sqlText = @"
                SELECT TOP 1 c.id, c.text, VectorDistance(c.embedding, @queryVector) AS similarityScore 
                FROM c ORDER BY VectorDistance(c.embedding, @queryVector)";
            
            QueryDefinition queryDef = new QueryDefinition(sqlText).WithParameter("@queryVector", queryVector);
            using FeedIterator<dynamic> iterator = docsContainer.GetItemQueryIterator<dynamic>(queryDef);
            
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    Console.WriteLine($"\tBest Match: {item.text} (Score: {item.similarityScore})");
                }
            }
        }
```

### 4. Update execution and Run
Call the new method in your top-level statements: `await RunSemanticSearchAsync(database);`.
Run the app using `dotnet run` and watch it successfully pair the search query with the document!
