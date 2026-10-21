namespace Versioner.Services.Email;

/// <summary>
/// 邮件(SMTP)相关配置，绑定自 appsettings.json 的 "Email" 节点。
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP 服务器地址。</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP 端口，默认 587(STARTTLS)。</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>是否使用 SSL/TLS。</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>登录用户名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>登录密码或授权码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>发件人显示名称。</summary>
    public string SenderName { get; set; } = "Versioner";

    /// <summary>发件人邮箱地址。</summary>
    public string SenderAddress { get; set; } = string.Empty;
}
