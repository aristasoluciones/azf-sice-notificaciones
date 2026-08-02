namespace sice.Functions.Notificaciones.Models
{
    /// <summary>
    /// Mensaje que el panel deja en la cola de SMS. El panel no conoce las
    /// credenciales del proveedor: solo indica a quién y qué se envía.
    /// </summary>
    public class SmsQueueMessage
    {
        public int IdNotificacion { get; set; }
        public int IdConvocatoria { get; set; }
        public string Destinatario { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    /// <summary>Respuesta del proveedor de SMS.</summary>
    public class SmsProveedorRespuesta
    {
        public int ErrorCode { get; set; }
        public string? ErrorDescription { get; set; }
    }
}
