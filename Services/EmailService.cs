using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sice.Functions.Notificaciones.Models;
using System.Net;
using System.Net.Mail;

namespace sice.Functions.Notificaciones.Services
{
    public class EmailService: IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task EnviarEmailAsync(EmailQueueMessage datos, Stream adjunto=null) 
        {
            using var smtp = new SmtpClient(_config["Smtp:Host"])
            {
                Port = int.Parse(_config["Smtp:Port"]!),
                Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"]),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_config["Smtp:Remitente"]!, _config["Smtp:Nombre"]!),
                Subject = datos.Asunto,
                Body = datos.CuerpoHtml,
                IsBodyHtml = true
            };
            mail.To.Add(datos.Destinatario);

            if (adjunto != null && !string.IsNullOrEmpty(datos.NombreBlobAdjunto))
            {
                mail.Attachments.Add(new Attachment(adjunto, datos.NombreBlobAdjunto));
            }

            await smtp.SendMailAsync(mail);
        }
    }
}
