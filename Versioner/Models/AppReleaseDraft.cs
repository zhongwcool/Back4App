namespace Versioner.Models;

public sealed class AppReleaseDraft
{
    public required string Version { get; init; }

    public required IReadOnlyList<string> Lines { get; init; }

    public required string Notes { get; init; }

    public required string DownloadUrl { get; init; }

    public required bool Mandatory { get; init; }

    public required bool Enabled { get; init; }
}
