namespace Versioner.Options;

public sealed class Back4AppOptions
{
    public const string SectionName = "Back4App";

    public string ApplicationId { get; set; } = "";

    public string MasterKey { get; set; } = "";

    public string ServerUrl { get; set; } = "https://parseapi.back4app.com";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApplicationId)
        && !string.IsNullOrWhiteSpace(MasterKey)
        && !string.IsNullOrWhiteSpace(ServerUrl);
}
