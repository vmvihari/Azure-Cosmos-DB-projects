using Microsoft.Azure.Cosmos;
using Azure;
using Azure.AI.OpenAI;
using System.Linq;
using System.Collections.Generic;

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
    ApplicationRegion = Regions.EastUS,
    AllowBulkExecution = true, // BEST PRACTICE: Optimizes throughput for bulk inserts
    MaxRetryAttemptsOnRateLimitedRequests = 9,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
};

// BEST PRACTICE: The CosmosClient should be a Singleton for the lifetime of your application.
using CosmosClient client = new CosmosClient(connectionString, options);

Database database = client.GetDatabase("AI-demo");
Container productsContainer = database.GetContainer("products");

await RunBasicQueryAsync(productsContainer);

await RunBulkInsertExperimentAsync(productsContainer);

await RunSemanticSearchAsync(database);

Container leasesContainer = database.GetContainer("leases");
await StartChangeFeedProcessorAsync(productsContainer, leasesContainer);

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

static async Task HandleChangesAsync(IReadOnlyCollection<dynamic> changes, CancellationToken cancellationToken)
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