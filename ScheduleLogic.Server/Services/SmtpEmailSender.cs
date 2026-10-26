using System.Net.Mail;
using System.Net;
using ScheduleLogic.Server.Services.Interfaces;

namespace ScheduleLogic.Server.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        public SmtpEmailSender(IConfiguration config) => _config = config;

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            using var message = new MailMessage(
                _config["MailSend:From"] ?? "noreply@schedulelogic.hu", to, subject, htmlBody)
            { IsBodyHtml = true };

            using var client = new SmtpClient(_config["MailSend:Host"])
            {
                Port = int.Parse(_config["MailSend:Port"] ?? "587"),
                Credentials = new NetworkCredential(_config["MailSend:User"], _config["MailSend:Password"]),
                EnableSsl = true
            };
            await client.SendMailAsync(message);
        }
    }
}
