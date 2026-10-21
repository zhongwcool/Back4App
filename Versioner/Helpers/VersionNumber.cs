namespace Versioner.Helpers;

public static class VersionNumber
{
    public static bool TryNormalize(string? text, out Version version, out string normalized)
    {
        version = new Version(0, 0);
        normalized = "";
        text = text?.Trim() ?? "";
        if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
            text = text[1..];

        if (!Version.TryParse(text, out var parsed))
            return false;

        version = new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));
        normalized = version.ToString();
        return true;
    }
}
