using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using VetFlow.Application.Common;
using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Web.Helpers;

/// <summary>Presentation helpers for Razor views.</summary>
public static class Ui
{
    public static string Money(decimal amount) => VetFlow.Application.Common.Money.Format(amount);
    public static string Percent(decimal rate) => VetFlow.Application.Common.Money.Percent(rate);
    public static string Phone(string? e164) => TurkishPhone.Format(e164);

    public static string Date(DateTime? value) => value?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "—";
    public static string DateAndTime(DateTime? value) => value?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—";
    public static string Time(DateTime value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string Quantity(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string AppointmentBadge(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "badge-soft-primary",
        AppointmentStatus.CheckedIn => "badge-soft-warning",
        AppointmentStatus.InProgress => "badge-soft-info",
        AppointmentStatus.Completed => "badge-soft-success",
        AppointmentStatus.NoShow => "badge-soft-danger",
        _ => "badge-soft-secondary"
    };

    public static string InvoiceBadge(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "badge-soft-secondary",
        InvoiceStatus.Issued => "badge-soft-primary",
        InvoiceStatus.PartiallyPaid => "badge-soft-warning",
        InvoiceStatus.Paid => "badge-soft-success",
        _ => "badge-soft-danger"
    };

    public static string DueBadge(VaccinationDueStatus status) => status switch
    {
        VaccinationDueStatus.Overdue => "badge-soft-danger",
        VaccinationDueStatus.DueSoon => "badge-soft-warning",
        _ => "badge-soft-success"
    };

    public static string SpeciesIcon(Species species) => species switch
    {
        Species.Dog => "bi-heart-pulse",
        Species.Cat => "bi-moon-stars",
        Species.Bird => "bi-feather",
        Species.Rabbit => "bi-flower1",
        Species.Reptile => "bi-shield-shaded",
        _ => "bi-circle"
    };

    public static string SexSymbol(Sex sex, bool neutered) => (sex switch
    {
        Sex.Male => "♂ Male",
        Sex.Female => "♀ Female",
        _ => "Unknown sex"
    }) + (neutered ? (sex == Sex.Female ? ", spayed" : ", neutered") : "");

    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static IReadOnlyList<SelectListItem> EnumOptions<TEnum>(TEnum? selected = null) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(v => new SelectListItem(DisplayName(v), v.ToString(),
                selected.HasValue && EqualityComparer<TEnum>.Default.Equals(v, selected.Value)))
            .ToList();

    public static string WithQuery(this HttpRequest request, string key, string value)
    {
        var builder = new QueryBuilder(request.Query
            .Where(q => !string.Equals(q.Key, key, StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? string.Empty))));
        builder.Add(key, value);
        return request.PathBase + request.Path + builder.ToQueryString();
    }

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
