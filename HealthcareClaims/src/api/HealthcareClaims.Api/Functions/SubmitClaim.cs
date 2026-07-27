using System.Net;
using System.Text.Json;
using HealthcareClaims.Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HealthcareClaims.Api.Functions
{
    public class SubmitClaim
    {
        private readonly ILogger _logger;

        public SubmitClaim(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubmitClaim>();
        }

        [Function("SubmitClaim")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims")] HttpRequestData req,
            [CosmosDBOutput("HealthcareDB", "Claims", Connection = "CosmosDbConnectionString")] IAsyncCollector<Claim> claimsOut)
        {
            _logger.LogInformation("Processing a new claim submission.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var claim = JsonSerializer.Deserialize<Claim>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (claim == null || string.IsNullOrEmpty(claim.ProviderId))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invalid claim data. ProviderId is required.");
                    return badReq;
                }

                // 1. Generate Synthetic Partition Key based on best practices
                claim.GeneratePartitionKey();

                // 2. Output Binding automatically handles the insert to Cosmos DB.
                // It's incredibly efficient as it uses the SDK under the hood.
                await claimsOut.AddAsync(claim);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(claim);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the claim.");
                return errorResponse;
            }
        }
    }
}
