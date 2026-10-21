using Versioner.Models;

namespace Versioner.Services.Parse;

public interface IAppReleaseClient
{
    bool IsConfigured { get; }

    /// <summary>读取版本记录。<paramref name="report"/> 在调用方上下文同步回调，用于加载步骤。</summary>
    Task<IReadOnlyList<AppReleaseRecord>> ListAsync(
        Action<string>? report = null,
        CancellationToken cancellationToken = default);

    Task<string> CreateAsync(AppReleaseDraft draft, CancellationToken cancellationToken = default);

    Task UpdateAsync(string objectId, AppReleaseDraft draft, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectId, CancellationToken cancellationToken = default);
}
