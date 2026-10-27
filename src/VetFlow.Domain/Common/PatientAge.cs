namespace VetFlow.Domain.Common;

public static class PatientAge
{
    /// <summary>Human-friendly age as used on the patient card: "3 y 2 m", "7 months", "3 weeks".</summary>
    public static string Describe(DateTime? dateOfBirth, DateTime today)
    {
        if (dateOfBirth is null)
        {
            return "Unknown";
        }

        var birth = dateOfBirth.Value.Date;
        if (birth > today.Date)
        {
            return "Unknown";
        }

        var months = (today.Year - birth.Year) * 12 + today.Month - birth.Month;
        if (today.Day < birth.Day)
        {
            months--;
        }

        if (months < 1)
        {
            var weeks = (int)((today.Date - birth).TotalDays / 7);
            return weeks <= 1 ? $"{(int)(today.Date - birth).TotalDays} days" : $"{weeks} weeks";
        }

        if (months < 12)
        {
            return months == 1 ? "1 month" : $"{months} months";
        }

        var years = months / 12;
        var rest = months % 12;
        return rest == 0 ? $"{years} y" : $"{years} y {rest} m";
    }
}
