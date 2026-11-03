using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Security;

namespace AcxiomCRM.Areas.Identity.Pages.Account;

public sealed class RegisterModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ApplicationDbContext db, ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();
        var email = Input.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var claimResult = await userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, Input.Name.Trim()));
        if (!claimResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in claimResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var roleResult = await userManager.AddToRoleAsync(user, RoleNames.SalesExecutive);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "Register", EntityName = "User", RecordId = user.Id, NewValue = $"Role={RoleNames.SalesExecutive}", CreatedDate = DateTime.UtcNow, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await db.SaveChangesAsync();
        logger.LogInformation("A new SalesExecutive account was registered.");
        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) || !Url.IsLocalUrl(ReturnUrl) ? "/" : ReturnUrl);
    }

    public sealed class InputModel
    {
        [Required, StringLength(100, MinimumLength = 2)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
