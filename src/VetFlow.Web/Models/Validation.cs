using System.ComponentModel.DataAnnotations;
using VetFlow.Domain.Common;

namespace VetFlow.Web.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class TurkishPhoneAttribute() : ValidationAttribute("{0} must be a Turkish phone number, e.g. 0532 123 45 67.")
{
    public override bool IsValid(object? value) =>
        value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || TurkishPhone.IsValid(s)));
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class MicrochipAttribute() : ValidationAttribute("{0} must have exactly 15 digits (ISO 11784).")
{
    public override bool IsValid(object? value) =>
        value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || MicrochipNumber.IsValid(s)));
}
