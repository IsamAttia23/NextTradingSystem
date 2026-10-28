using System.Text.RegularExpressions;
namespace NEXT.web.Services;
public static partial class BadgeColors
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")] private static partial Regex HexColor();
    public static string Safe(string value) => HexColor().IsMatch(value) ? value : value.ToLowerInvariant() switch
    { "blue" => "#153d97", "green" => "#147550", "red" => "#b42339", "yellow" => "#f5d446", _ => "#64748b" };
    public static string Foreground(string color)
    {
        var hex = Safe(color);
        var brightness = (Convert.ToInt32(hex[1..3], 16) * 299 + Convert.ToInt32(hex[3..5], 16) * 587 + Convert.ToInt32(hex[5..7], 16) * 114) / 1000;
        return brightness > 155 ? "#13244b" : "#ffffff";
    }
}
