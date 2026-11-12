using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using StorEase.Application.Common;

namespace StorEase.Application.Tests;

/// <summary>API errors follow the request's language; English text in the code is the key.</summary>
public class ErrorTextTests
{
    private static string In(string language, string message)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(language);
        try { return ErrorText.Localize(message); }
        finally { CultureInfo.CurrentUICulture = before; }
    }

    [Fact]
    public void Vietnamese_requests_get_Vietnamese_and_others_keep_the_English()
    {
        In("vi", "Contract not found.").Should().Be("Không tìm thấy hợp đồng.");
        In("en", "Contract not found.").Should().Be("Contract not found.");
        In("vi", "A message nobody translated.").Should().Be("A message nobody translated.");
    }

    [Fact]
    public void Values_stay_and_a_status_among_them_is_translated()
    {
        In("vi", "Unit A-101 is Occupied, not Available.").Should().Be("Kho A-101 đang ở trạng thái đang thuê, không còn trống.");
        In("vi", "Overlaps shift #7 at Gò Vấp (2026-11-01 07:00-15:00).").Should().Be("Trùng ca #7 tại Gò Vấp (2026-11-01 07:00-15:00).");
    }

    [Fact]
    public void Every_message_in_the_code_has_a_Vietnamese_translation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root.GetDirectories("StorEase.Application").Length == 0)
            root = root.Parent!;
        var call = new Regex(@"(?:\bError\.\w+|\.WithMessage)\(\s*(\$?)""((?:[^""\\]|\\.)*)""");
        var messages = root.GetDirectories("StorEase.*")
            .SelectMany(d => d.GetFiles("*.cs", SearchOption.AllDirectories))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Name.EndsWith("Designer.cs"))
            .Select(f => Regex.Replace(File.ReadAllText(f.FullName), @"""\s*\+\s*\$?""", "")) // a message split over two lines
            .SelectMany(text => call.Matches(text))
            .Select(m => m.Groups[1].Value == "$" ? Regex.Replace(m.Groups[2].Value, @"\{[^{}]*\}", "Active") : m.Groups[2].Value)
            .Distinct().ToList();

        messages.Should().HaveCountGreaterThan(150, "the scan must really find the messages");
        messages.Where(m => In("vi", m) == m).Should().BeEmpty();
    }
}
