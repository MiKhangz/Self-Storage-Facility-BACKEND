using System.Globalization;

namespace StorEase.Application.Common;

public static class Money
{
    private static readonly NumberFormatInfo Dots = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };

    /// <summary>For Vietnamese message text: 1.600.000, whatever the server's culture.</summary>
    public static string Text(decimal value) => value.ToString("N0", Dots);

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
