namespace Versioner.ViewModels;

/// <summary>MaterialDesign DialogHost 确认框内容。</summary>
public sealed class ConfirmPrompt
{
    public required string Title { get; init; }

    public required string Message { get; init; }
}
