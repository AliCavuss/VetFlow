using System.Globalization;

namespace VetFlow.Application.Common;

/// <summary>Formats amounts the Turkish way: 1.234,50 ₺.</summary>
public static class Money
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(decimal amount) => amount.ToString("#,##0.00", Turkish) + " ₺";

    public static string Percent(decimal rate) => "%" + (rate * 100).ToString("0.##", Turkish);
}
