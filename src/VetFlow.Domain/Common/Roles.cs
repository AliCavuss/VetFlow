namespace VetFlow.Domain.Common;

public static class Roles
{
    /// <summary>Clinic manager: everything, including inventory, staff calendars and price lists.</summary>
    public const string Admin = "Admin";

    /// <summary>Examines patients, writes medical records, administers vaccines.</summary>
    public const string Veterinarian = "Veterinarian";

    /// <summary>Front desk: owners, patients, appointments, invoices and payments.</summary>
    public const string Receptionist = "Receptionist";

    public static readonly string[] All = [Admin, Veterinarian, Receptionist];
}
