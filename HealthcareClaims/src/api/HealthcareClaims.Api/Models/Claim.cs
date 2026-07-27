using System.Text.Json.Serialization;

namespace HealthcareClaims.Api.Models
{
    public class Claim
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // The Partition Key property
        [JsonPropertyName("PartitionKey")]
        public string PartitionKey { get; set; } = string.Empty;

        public string ProviderId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        
        public DateTime SubmittedDate { get; set; }
        
        public string Status { get; set; } = "Pending";

        public decimal TotalAmount { get; set; }

        public List<ClaimLineItem> LineItems { get; set; } = new List<ClaimLineItem>();
        
        // This will be excluded from the index to save RUs
        public string MedicalNotes { get; set; } = string.Empty;

        // Helper to generate the synthetic partition key
        public void GeneratePartitionKey()
        {
            PartitionKey = $"{ProviderId}_{SubmittedDate:yyyyMM}";
        }
    }

    public class ClaimLineItem
    {
        public string ProcedureCode { get; set; } = string.Empty;
        public string DiagnosisCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
