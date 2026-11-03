using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer
{
    public int CustomerId { get; set; }
    [Required, StringLength(30)] public string CustomerCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [StringLength(30)]
    [RegularExpression(@"^[0-9+()\-\.\s]{7,30}$", ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Required, StringLength(450)] public string CreatedBy { get; set; } = string.Empty;
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
