using System.Security.Claims;
using VetFlow.Domain.Common;

namespace VetFlow.Web.Helpers;

public static class Policies
{
    /// <summary>Medical records, treatments, vaccinations.</summary>
    public const string Clinical = nameof(Clinical);

    /// <summary>Invoices and payments.</summary>
    public const string Billing = nameof(Billing);

    /// <summary>Price list and stock.</summary>
    public const string ManageInventory = nameof(ManageInventory);
}

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
    public static bool IsClinical(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Veterinarian);
    public static bool IsBilling(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Receptionist);

    public static string UserName(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? throw new InvalidOperationException("User is not signed in.");
}
