using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }
    [Required, StringLength(150)] public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal Amount { get; set; }
    [Required, StringLength(50)] public string Stage { get; set; } = "Prospecting";
    [Range(0, 100)] public decimal Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Open";
    [StringLength(450)] public string? AssignedTo { get; set; }
    public Customer Customer { get; set; } = null!;
    public Lead? Lead { get; set; }
}
