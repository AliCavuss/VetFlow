using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>Outgoing SMS reminder (outbox). <see cref="DedupKey"/> guarantees one reminder per event.</summary>
public class Reminder
{
    public long Id { get; set; }
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public int? PatientId { get; set; }
    public ReminderType Type { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>e.g. "vaccination:42" or "appointment:17".</summary>
    public string DedupKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public string? Error { get; set; }
}
