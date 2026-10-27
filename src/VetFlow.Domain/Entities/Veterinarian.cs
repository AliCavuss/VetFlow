namespace VetFlow.Domain.Entities;

public class Veterinarian
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>e.g. "DVM", "DVM, PhD".</summary>
    public string Title { get; set; } = "DVM";

    public string? Specialty { get; set; }

    /// <summary>Colour used for this veterinarian's appointments in the calendar (hex).</summary>
    public string CalendarColor { get; set; } = "#2f6f4f";

    public bool IsActive { get; set; } = true;

    public string DisplayName => $"Dr. {FullName}";
}
