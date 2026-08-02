using sice.Functions.Notificaciones.Models;

namespace sice.Functions.Notificaciones.Services
{
    public interface ISmsService
    {
        /// <summary>Envía el mensaje al proveedor. True si el proveedor lo aceptó.</summary>
        Task<bool> EnviarSmsAsync(SmsQueueMessage datos);
    }
}
