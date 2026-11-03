using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class CustomerFormViewModel
{
    public int CustomerId { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Customer code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    [Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [RegularExpression(@"^[0-9+()\-\.\s]{7,30}$", ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }

    [StringLength(150)]
    [Display(Name = "Company name")]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [Required, StringLength(30)]
    public string Status { get; set; } = "Active";

    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
}
