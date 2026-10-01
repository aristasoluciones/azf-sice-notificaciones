namespace sice.Functions.Notificaciones.Services
{
    public interface INotificacionRepositorio
    {
        /// <summary>Estatus actual de la notificación; null si no existe.</summary>
        Task<string?> GetStatusAsync(int idNotificacion);

        /// <summary>Deja el resultado del envío ("OK" o "ERROR").</summary>
        Task RegistrarEnvioAsync(int idNotificacion, string status);

        /// <summary>
        /// Estatus del envío de un renglón de difusión por un medio ("EMAIL" o "SMS");
        /// null si el renglón no existe.
        /// </summary>
        Task<string?> GetStatusDifusionAsync(int idDetalle, string medio);

        /// <summary>Deja el resultado del envío de un renglón de difusión ("OK" o "ERROR").</summary>
        Task RegistrarEnvioDifusionAsync(int idDetalle, string medio, string status);
    }
}
