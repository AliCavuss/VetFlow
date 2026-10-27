using Microsoft.AspNetCore.Identity;

namespace VetFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Set for users who are veterinarians – their calendar is shown by default.</summary>
    public int? VeterinarianId { get; set; }
}
