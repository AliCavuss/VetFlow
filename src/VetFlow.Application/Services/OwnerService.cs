using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Common;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;

namespace VetFlow.Application.Services;

public sealed record OwnerInput(
    string FirstName, string LastName, string Phone, string? Email, string? Address, bool ReminderConsent, string? Notes);

public sealed class OwnerListItem
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? Email { get; init; }
    public bool ReminderConsent { get; init; }
    public IReadOnlyList<string> PatientNames { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}

public interface IOwnerService
{
    Task<PagedResult<OwnerListItem>> SearchAsync(string? query, int page, CancellationToken ct = default);
    Task<Owner> GetAsync(int id, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, int? excludeId, CancellationToken ct = default);
    Task<Owner> CreateAsync(OwnerInput input, CancellationToken ct = default);
    Task UpdateAsync(int id, OwnerInput input, CancellationToken ct = default);
}

public sealed class OwnerService(IApplicationDbContext db, IClinicClock clock) : IOwnerService
{
    public Task<PagedResult<OwnerListItem>> SearchAsync(string? query, int page, CancellationToken ct = default)
    {
        var owners = db.Owners.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            owners = owners.Where(o =>
                o.FirstName.Contains(term) || o.LastName.Contains(term)
                || (o.FirstName + " " + o.LastName).Contains(term)
                || (digits.Length >= 3 && o.Phone.Contains(digits))
                || o.Patients.Any(p => p.Name.Contains(term) || p.MicrochipNumber == term));
        }

        var projected = owners
            .OrderBy(o => o.LastName).ThenBy(o => o.FirstName)
            .Select(o => new OwnerListItem
            {
                Id = o.Id,
                FullName = o.FirstName + " " + o.LastName,
                Phone = o.Phone,
                Email = o.Email,
                ReminderConsent = o.ReminderConsent,
                PatientNames = o.Patients.OrderBy(p => p.Name).Select(p => p.Name).ToList(),
                CreatedAt = o.CreatedAt
            });

        return PagedResult<OwnerListItem>.CreateAsync(projected, page, 20, ct);
    }

    public async Task<Owner> GetAsync(int id, CancellationToken ct = default) =>
        await db.Owners.AsNoTracking()
            .Include(o => o.Patients)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException(nameof(Owner), id);

    public Task<bool> PhoneExistsAsync(string phone, int? excludeId, CancellationToken ct = default)
    {
        if (!TurkishPhone.TryNormalize(phone, out var normalized))
        {
            return Task.FromResult(false);
        }

        return db.Owners.AnyAsync(o => o.Phone == normalized && (excludeId == null || o.Id != excludeId), ct);
    }

    public async Task<Owner> CreateAsync(OwnerInput input, CancellationToken ct = default)
    {
        var owner = new Owner { CreatedAt = clock.Now };
        Apply(owner, input);
        db.Owners.Add(owner);
        await db.SaveChangesAsync(ct);
        return owner;
    }

    public async Task UpdateAsync(int id, OwnerInput input, CancellationToken ct = default)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Owner), id);
        Apply(owner, input);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Owner owner, OwnerInput input)
    {
        owner.FirstName = input.FirstName.Trim();
        owner.LastName = input.LastName.Trim();
        owner.Phone = TurkishPhone.Normalize(input.Phone);
        owner.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        owner.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
        owner.ReminderConsent = input.ReminderConsent;
        owner.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }
}
