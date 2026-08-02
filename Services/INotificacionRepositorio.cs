namespace sice.Functions.Notificaciones.Services
{
    public interface INotificacionRepositorio
    {
        /// <summary>Estatus actual de la notificación; null si no existe.</summary>
        Task<string?> GetStatusAsync(int idNotificacion);

        /// <summary>Deja el resultado del envío ("OK" o "ERROR").</summary>
        Task RegistrarEnvioAsync(int idNotificacion, string status);
    }
}
