using System.Net.Mail;

namespace StreamBG.API.Services;

public interface IEmailService
{
    Task SendStreamStartedAsync(string toEmail, string toUsername, string streamerName, string streamTitle, string streamUrl);
    Task SendDonationReceivedAsync(string toEmail, string toUsername, string donorName, decimal amount, string? message);
    Task SendNewSubscriberAsync(string toEmail, string toUsername, string subscriberName, string planName, decimal price);
    Task SendSubscriptionExpiringAsync(string toEmail, string toUsername, string streamerName, DateTime expiresAt);
    Task SendWelcomeAsync(string toEmail, string username);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendStreamStartedAsync(
        string toEmail, string toUsername, string streamerName,
        string streamTitle, string streamUrl)
    {
        var subject = $"🔴 {streamerName} е на живо!";
        var html = StreamStartedTemplate(toUsername, streamerName, streamTitle, streamUrl);
        await SendAsync(toEmail, subject, html);
    }

    public async Task SendDonationReceivedAsync(
        string toEmail, string toUsername, string donorName, decimal amount, string? message)
    {
        var subject = $"💜 Получи дарение от {donorName} — {amount:F2} лв.";
        var html = DonationTemplate(toUsername, donorName, amount, message);
        await SendAsync(toEmail, subject, html);
    }

    public async Task SendNewSubscriberAsync(
        string toEmail, string toUsername, string subscriberName, string planName, decimal price)
    {
        var subject = $"🎉 {subscriberName} се абонира!";
        var html = NewSubscriberTemplate(toUsername, subscriberName, planName, price);
        await SendAsync(toEmail, subject, html);
    }

    public async Task SendSubscriptionExpiringAsync(
        string toEmail, string toUsername, string streamerName, DateTime expiresAt)
    {
        var subject = $"⚠️ Абонаментът ти за {streamerName} изтича скоро";
        var html = ExpiringTemplate(toUsername, streamerName, expiresAt);
        await SendAsync(toEmail, subject, html);
    }

    public async Task SendWelcomeAsync(string toEmail, string username)
    {
        var subject = "Добре дошъл в StreamBG! 🎥";
        var html = WelcomeTemplate(username);
        await SendAsync(toEmail, subject, html);
    }

    private async Task SendAsync(string to, string subject, string html)
    {
        var smtpHost = _config["Email:SmtpHost"];

        if (string.IsNullOrEmpty(smtpHost))
        {
            _logger.LogInformation("📧 EMAIL (dev) -> {To}\n  Subject: {Subject}", to, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(smtpHost)
            {
                Port = int.Parse(_config["Email:SmtpPort"] ?? "587"),
                Credentials = new System.Net.NetworkCredential(
                    _config["Email:Username"],
                    _config["Email:Password"]),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mail = new MailMessage
            {
                From = new MailAddress(
                    _config["Email:FromAddress"] ?? "noreply@streambg.bg",
                    "StreamBG"),
                Subject = subject,
                Body = html,
                IsBodyHtml = true,
            };
            mail.To.Add(to);

            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при изпращане на email до {To}", to);
        }
    }

    private static string WrapHtml(string content)
    {
        return "<!DOCTYPE html>\n" +
               "<html lang=\"bg\">\n" +
               "<head>\n" +
               "  <meta charset=\"UTF-8\"/>\n" +
               "  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>\n" +
               "  <style>\n" +
               "    body { font-family: 'Segoe UI', Helvetica, Arial, sans-serif;\n" +
               "           background: #0e0e10; color: #efeff1; margin: 0; padding: 0; }\n" +
               "    .wrapper { max-width: 560px; margin: 0 auto; padding: 24px 16px; }\n" +
               "    .logo { font-size: 22px; font-weight: 800; color: #a970ff; margin-bottom: 28px; }\n" +
               "    .logo span { background: #a970ff; color: white; padding: 4px 8px;\n" +
               "                 border-radius: 6px; margin-right: 6px; }\n" +
               "    .card { background: #18181b; border-radius: 12px; padding: 28px;\n" +
               "            border: 1px solid rgba(255,255,255,0.08); }\n" +
               "    h1 { font-size: 22px; margin: 0 0 10px; }\n" +
               "    p { color: #adadb8; line-height: 1.6; margin: 8px 0; font-size: 15px; }\n" +
               "    .btn { display: inline-block; background: #a970ff; color: white !important;\n" +
               "           padding: 12px 28px; border-radius: 8px; text-decoration: none;\n" +
               "           font-weight: 700; font-size: 15px; margin: 20px 0; }\n" +
               "    .highlight { color: #efeff1; font-weight: 600; }\n" +
               "    .amount { font-size: 32px; font-weight: 800; color: #a970ff; }\n" +
               "    .footer { color: #5a5a6a; font-size: 12px; margin-top: 24px; text-align: center; }\n" +
               "    .badge { display: inline-block; background: #1f1f23; border: 1px solid rgba(255,255,255,0.1);\n" +
               "             border-radius: 999px; padding: 4px 12px; font-size: 13px; color: #adadb8; }\n" +
               "  </style>\n" +
               "</head>\n" +
               "<body>\n" +
               "  <div class=\"wrapper\">\n" +
               "    <div class=\"logo\"><span>&#9654;</span>StreamBG</div>\n" +
               content + "\n" +
               "    <div class=\"footer\">\n" +
               "      Получаваш този имейл, защото си регистриран в StreamBG.<br/>\n" +
               "      <a href=\"https://streambg.bg/settings\" style=\"color:#5a5a6a\">Управление на нотификациите</a>\n" +
               "    </div>\n" +
               "  </div>\n" +
               "</body>\n" +
               "</html>";
    }

    private static string StreamStartedTemplate(string username, string streamer, string title, string url)
    {
        var body = "<div class=\"card\">\n" +
                   $"  <h1>🔴 {streamer} е на живо!</h1>\n" +
                   $"  <p>Здравей <span class=\"highlight\">{username}</span>, стриймърът, когото следваш, стартира стрийм.</p>\n" +
                   $"  <p style=\"font-size:18px; font-weight:600; color:#efeff1; margin: 16px 0;\">\"{title}\"</p>\n" +
                   $"  <a href=\"{url}\" class=\"btn\">Гледай сега →</a>\n" +
                   "  <p style=\"margin-top:16px\"><span class=\"badge\">На живо</span></p>\n" +
                   "</div>";
        return WrapHtml(body);
    }

    private static string DonationTemplate(string username, string donor, decimal amount, string? message)
    {
        var msgBlock = message is not null
            ? $"<p style='background:#1f1f23;padding:12px 16px;border-radius:8px;color:#efeff1;margin:16px 0;'>„{message}\"</p>"
            : "";
        var body = "<div class=\"card\">\n" +
                   "  <h1>💜 Получи дарение!</h1>\n" +
                   $"  <p>Здравей <span class=\"highlight\">{username}</span>!</p>\n" +
                   $"  <div class=\"amount\">{amount:F2} лв.</div>\n" +
                   $"  <p>от <span class=\"highlight\">{donor}</span></p>\n" +
                   $"  {msgBlock}\n" +
                   "  <a href=\"https://streambg.bg/dashboard\" class=\"btn\">Виж таблото →</a>\n" +
                   "</div>";
        return WrapHtml(body);
    }

    private static string NewSubscriberTemplate(string username, string subscriber, string plan, decimal price)
    {
        var body = "<div class=\"card\">\n" +
                   "  <h1>🎉 Нов абонат!</h1>\n" +
                   $"  <p>Здравей <span class=\"highlight\">{username}</span>!</p>\n" +
                   $"  <p><span class=\"highlight\">{subscriber}</span> се абонира за план</p>\n" +
                   $"  <p style=\"font-size:18px;color:#efeff1;font-weight:700;margin:10px 0;\">\n" +
                   $"    \"{plan}\" — {price:F2} лв./месец\n" +
                   "  </p>\n" +
                   "  <a href=\"https://streambg.bg/dashboard\" class=\"btn\">Виж абонатите →</a>\n" +
                   "</div>";
        return WrapHtml(body);
    }

    private static string ExpiringTemplate(string username, string streamer, DateTime expires)
    {
        var body = "<div class=\"card\">\n" +
                   "  <h1>⚠️ Абонаментът изтича скоро</h1>\n" +
                   $"  <p>Здравей <span class=\"highlight\">{username}</span>!</p>\n" +
                   $"  <p>Абонаментът ти за <span class=\"highlight\">{streamer}</span> изтича на\n" +
                   $"    <span class=\"highlight\">{expires:dd.MM.yyyy}</span>.</p>\n" +
                   "  <p>Не пропускай ексклузивното съдържание и предимства!</p>\n" +
                   "  <a href=\"https://streambg.bg/subscriptions\" class=\"btn\">Поднови абонамента →</a>\n" +
                   "</div>";
        return WrapHtml(body);
    }

    private static string WelcomeTemplate(string username)
    {
        var body = "<div class=\"card\">\n" +
                   "  <h1>Добре дошъл в StreamBG! 🎥</h1>\n" +
                   $"  <p>Здравей <span class=\"highlight\">{username}</span>!</p>\n" +
                   "  <p>Готов ли си да стриймваш или да гледаш любимите си стриймъри?</p>\n" +
                   "  <a href=\"https://streambg.bg\" class=\"btn\">Към платформата →</a>\n" +
                   "</div>";
        return WrapHtml(body);
    }
}
