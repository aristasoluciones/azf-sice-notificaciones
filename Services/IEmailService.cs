using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using sice.Functions.Notificaciones.Models;

namespace sice.Functions.Notificaciones.Services
{
    public interface IEmailService
    {

        public Task EnviarEmailAsync(EmailQueueMessage message, Stream adjunto = null);
    }
}
