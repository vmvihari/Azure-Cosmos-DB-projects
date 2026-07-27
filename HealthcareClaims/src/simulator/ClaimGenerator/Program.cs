using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClaimGenerator
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private const string apiUrl = "http://localhost:7071/api/claims"; // URL of local Azure Function

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Healthcare Claim Generator...");
            Console.WriteLine($"Targeting API: {apiUrl}");
            
            var random = new Random();
            var providers = new[] { "PRV-100", "PRV-200", "PRV-300", "PRV-400" };
            var statuses = new[] { "Submitted", "Pending", "Under Review", "Approved", "Rejected" };
            
            int count = 0;

            while (true)
            {
                var provider = providers[random.Next(providers.Length)];
                var status = statuses[random.Next(statuses.Length)];
                var amount = Math.Round(random.NextDouble() * 5000 + 100, 2);

                // Construct a mock claim mirroring the backend model
                var claim = new
                {
                    ProviderId = provider,
                    PatientId = $"PAT-{random.Next(1000, 9999)}",
                    SubmittedDate = DateTime.UtcNow,
                    Status = status,
                    TotalAmount = amount,
                    MedicalNotes = "Patient presented with typical symptoms. Routine checkup performed.",
                    LineItems = new[]
                    {
                        new { ProcedureCode = "99213", DiagnosisCode = "J01.90", Amount = amount * 0.6m },
                        new { ProcedureCode = "36415", DiagnosisCode = "Z00.00", Amount = amount * 0.4m }
                    }
                };

                var json = JsonSerializer.Serialize(claim);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        count++;
                        Console.WriteLine($"[{count}] Successfully submitted claim for {provider} | Amount: ${amount} | Status: {status}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to submit claim. Status: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error communicating with API: {ex.Message}");
                }

                // Simulate random interval between claims (enterprise load could be faster, but this is for local demo)
                await Task.Delay(random.Next(1000, 3000));
            }
        }
    }
}
