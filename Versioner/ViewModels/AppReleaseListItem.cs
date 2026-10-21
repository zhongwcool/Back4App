using Versioner.Models;

namespace Versioner.ViewModels;

public sealed class AppReleaseListItem
{
    public required AppReleaseRecord Record { get; init; }

    public Version? Version { get; init; }

    public required string VersionText { get; init; }

    public required string EnabledText { get; init; }

    public required string MandatoryText { get; init; }

    public required string DownloadUrl { get; init; }

    public required string NotesText { get; init; }

    /// <summary>表格单行展示，换行并成空格。</summary>
    public required string NotesPreview { get; init; }

    public required string UpdatedAtText { get; init; }

    public required string PickText { get; init; }
}
