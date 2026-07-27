using System.Text.Json;
using HealthcareClaims.Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HealthcareClaims.Api.Functions
{
    public class ClaimChangeFeed
    {
        private readonly ILogger _logger;

        public ClaimChangeFeed(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ClaimChangeFeed>();
        }

        // Cosmos DB Trigger: Listens to the Change Feed on the Claims container.
        // It uses the 'leases' container to maintain state (bookmarks) so it can scale out.
        [Function("ProcessClaimChanges")]
        [SignalROutput(HubName = "claimsHub", ConnectionStringSetting = "SignalRConnectionString")]
        public SignalRMessageAction Run(
            [CosmosDBTrigger(
                databaseName: "HealthcareDB",
                containerName: "Claims",
                Connection = "CosmosDbConnectionString",
                LeaseContainerName = "leases",
                CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Claim> input)
        {
            if (input != null && input.Count > 0)
            {
                _logger.LogInformation($"Change Feed triggered. Documents modified: {input.Count}");
                
                // For demonstration, we'll take the first changed claim and broadcast it.
                // In a real scenario, you might loop through and broadcast individual updates.
                var updatedClaim = input[0];
                
                _logger.LogInformation($"Broadcasting update for Claim: {updatedClaim.Id}, Status: {updatedClaim.Status}");

                // Send a message to all connected SignalR clients
                return new SignalRMessageAction("claimUpdated")
                {
                    Arguments = new[] { updatedClaim }
                };
            }
            
            return null; // No updates to send
        }
    }

    // Helper class for SignalR Output Binding
    public class SignalRMessageAction
    {
        public SignalRMessageAction(string target)
        {
            Target = target;
        }

        public string Target { get; set; }
        public object[] Arguments { get; set; }
    }
}
