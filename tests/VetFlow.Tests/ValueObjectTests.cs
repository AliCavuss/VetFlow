using VetFlow.Domain.Common;

namespace VetFlow.Tests;

public class TurkishPhoneTests
{
    [Theory]
    [InlineData("0532 123 45 67", "+905321234567")]
    [InlineData("+90 (532) 123-45-67", "+905321234567")]
    [InlineData("5321234567", "+905321234567")]
    [InlineData("0216 345 67 89", "+902163456789")]
    public void Normalizes_to_e164(string input, string expected) => Assert.Equal(expected, TurkishPhone.Normalize(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("0632 123 45 67")] // national numbers start with 2–5
    [InlineData("+49 151 2345678")]
    [InlineData("")]
    public void Rejects_invalid_numbers(string input) => Assert.False(TurkishPhone.IsValid(input));

    [Fact]
    public void Formats_for_display() => Assert.Equal("+90 532 123 45 67", TurkishPhone.Format("+905321234567"));
}

public class MicrochipNumberTests
{
    [Theory]
    [InlineData("792097000123456", true)]
    [InlineData("792 097 000 123 456", true)]
    [InlineData("79209700012345", false)]
    [InlineData("79209700012345X", false)]
    public void Validates_15_digit_iso_codes(string value, bool expected) => Assert.Equal(expected, MicrochipNumber.IsValid(value));

    [Fact]
    public void Describes_country_and_manufacturer_prefixes()
    {
        Assert.Equal("Country code 792", MicrochipNumber.DescribePrefix("792097000123456"));
        Assert.Equal("Manufacturer code 981", MicrochipNumber.DescribePrefix("981020004417721"));
    }
}

public class PatientAgeTests
{
    private static readonly DateTime Today = new(2026, 10, 22);

    [Theory]
    [InlineData(2019, 5, 12, "7 y 5 m")]
    [InlineData(2023, 10, 22, "3 y")]
    [InlineData(2026, 3, 30, "6 months")]
    [InlineData(2026, 9, 1, "1 month")]
    [InlineData(2026, 9, 25, "3 weeks")]
    [InlineData(2026, 10, 20, "2 days")]
    public void Describes_age(int y, int m, int d, string expected) =>
        Assert.Equal(expected, PatientAge.Describe(new DateTime(y, m, d), Today));

    [Fact]
    public void Unknown_when_missing_or_in_future()
    {
        Assert.Equal("Unknown", PatientAge.Describe(null, Today));
        Assert.Equal("Unknown", PatientAge.Describe(Today.AddDays(3), Today));
    }
}
