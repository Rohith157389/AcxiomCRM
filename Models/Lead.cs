using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead
{
    public int LeadId { get; set; }
    [Required, StringLength(30)] public string LeadCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string LeadName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [Required, StringLength(80)] public string Source { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "New";
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? AssignedTo { get; set; }
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
