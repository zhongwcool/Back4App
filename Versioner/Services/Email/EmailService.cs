using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Versioner.Services.Email;

/// <summary>
/// 基于 MailKit 的邮件发送服务实现。
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            throw new InvalidOperationException("尚未配置 SMTP 服务器(Email.SmtpHost)，无法发送邮件。");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.SenderName, _options.SenderAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = isHtml ? body : null,
            TextBody = isHtml ? null : body
        }.ToMessageBody();

        using var client = new SmtpClient();
        var secureOption = _options.UseSsl
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.None;

        try
        {
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureOption, cancellationToken);

            if (!string.IsNullOrEmpty(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation("邮件已发送至 {Recipient}，主题：{Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送邮件至 {Recipient} 失败。", to);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
