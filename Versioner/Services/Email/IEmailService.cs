namespace Versioner.Services.Email;

/// <summary>
/// 邮件发送服务抽象。
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// 发送一封纯文本或 HTML 邮件。
    /// </summary>
    Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = false,
        CancellationToken cancellationToken = default);
}
