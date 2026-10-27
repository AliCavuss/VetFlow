using System.ComponentModel.DataAnnotations;

namespace VetFlow.Domain.Enums;

public enum Species
{
    Dog = 1,
    Cat = 2,
    Bird = 3,
    Rabbit = 4,
    Rodent = 5,
    Reptile = 6,
    Other = 99
}

public enum Sex
{
    Unknown = 0,
    Male = 1,
    Female = 2
}

public enum AppointmentType
{
    [Display(Name = "Check-up")] Checkup = 1,
    Vaccination = 2,
    Surgery = 3,
    Dental = 4,
    Emergency = 5,
    [Display(Name = "Follow-up")] FollowUp = 6,
    Grooming = 7
}

public enum AppointmentStatus
{
    Scheduled = 1,
    [Display(Name = "Checked in")] CheckedIn = 2,
    [Display(Name = "In progress")] InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    [Display(Name = "No-show")] NoShow = 6
}

public enum VisitStatus
{
    Open = 1,
    Closed = 2
}

public enum ProductCategory
{
    Service = 1,
    Medication = 2,
    Vaccine = 3,
    Supply = 4,
    Food = 5
}

public enum StockMovementType
{
    Purchase = 1,
    Usage = 2,
    Adjustment = 3,
    [Display(Name = "Usage reversed")] UsageReversed = 4
}

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    [Display(Name = "Partially paid")] PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public enum PaymentMethod
{
    Cash = 1,
    [Display(Name = "Credit card")] CreditCard = 2,
    [Display(Name = "Bank transfer")] BankTransfer = 3
}

public enum VaccinationDueStatus
{
    [Display(Name = "Up to date")] UpToDate = 1,
    [Display(Name = "Due soon")] DueSoon = 2,
    Overdue = 3
}

public enum ReminderType
{
    [Display(Name = "Vaccination due")] VaccinationDue = 1,
    [Display(Name = "Appointment tomorrow")] AppointmentTomorrow = 2
}

public enum ReminderStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

public static class AppointmentTypeExtensions
{
    /// <summary>Default slot length used to pre-fill the booking form.</summary>
    public static int DefaultDurationMinutes(this AppointmentType type) => type switch
    {
        AppointmentType.Vaccination or AppointmentType.FollowUp => 15,
        AppointmentType.Checkup or AppointmentType.Emergency => 30,
        AppointmentType.Grooming => 45,
        AppointmentType.Dental => 60,
        AppointmentType.Surgery => 90,
        _ => 30
    };
}
