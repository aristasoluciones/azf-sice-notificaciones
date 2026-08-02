using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using sice.Functions.Notificaciones.Models;
using sice.Functions.Notificaciones.Services;

namespace sice.Functions.Notificaciones;

/// <summary>
/// Toma los SMS que el panel dejó en la cola, los envía al proveedor y deja el
/// resultado en la base de datos.
/// Cola propia, separada de la de correos: si el proveedor de SMS falla y sus
/// mensajes se quedan reintentando, el correo sigue saliendo.
/// </summary>
public class SmsFunction
{
    private readonly ISmsService _smsService;
    private readonly INotificacionRepositorio _repositorio;
    private readonly ILogger<SmsFunction> _logger;

    public SmsFunction(ISmsService smsService, INotificacionRepositorio repositorio, ILogger<SmsFunction> logger)
    {
        _smsService = smsService;
        _repositorio = repositorio;
        _logger = logger;
    }

    [Function("ProcesarSms")]
    public async Task Run([QueueTrigger("%Queue:NombreColaSms%", Connection = "StorageNegocioConnection")] SmsQueueMessage datos)
    {
        // Cada intento de SMS se cobra: si la cola reprocesa un mensaje ya
        // enviado, el estatus lo delata y aquí se detiene.
        string? status = await _repositorio.GetStatusAsync(datos.IdNotificacion);

        if (status == null)
        {
            _logger.LogWarning("La notificación {Id} no existe; se descarta el mensaje.", datos.IdNotificacion);
            return;
        }

        if (status != "ENC")
        {
            _logger.LogWarning("La notificación {Id} ya está en estatus '{Status}'; no se reenvía.",
                datos.IdNotificacion, status);
            return;
        }

        bool enviado;
        try
        {
            enviado = await _smsService.EnviarSmsAsync(datos);
        }
        catch (Exception ex)
        {
            // Falla de comunicación: se deja que la cola reintente, el estatus
            // sigue en 'ENC' y la comprobación de arriba evita el doble envío
            // cuando el mensaje sí llegó a salir.
            _logger.LogError(ex, "Error al contactar al proveedor de SMS para la notificación {Id}.", datos.IdNotificacion);
            throw;
        }

        // El proveedor respondió: el resultado es definitivo, no se reintenta.
        await _repositorio.RegistrarEnvioAsync(datos.IdNotificacion, enviado ? "OK" : "ERROR");
    }
}
