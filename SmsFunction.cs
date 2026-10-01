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
/// El mensaje puede venir de una notificación externa (IdNotificacion) o de un
/// renglón de difusión (IdDifusionDetalle); el control es el mismo para ambos.
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
    public async Task Run([QueueTrigger("%Queue:NombreColaSms%", Connection = "StorageNegocioConnection")] SmsQueueMessage datos, FunctionContext context)
    {
        bool esDifusion = datos.IdDifusionDetalle.HasValue;
        int id = datos.IdDifusionDetalle ?? datos.IdNotificacion;
        string origen = esDifusion ? "difusión" : "notificación";

        // Cada intento de SMS se cobra: si la cola reprocesa un mensaje ya
        // enviado, el estatus lo delata y aquí se detiene.
        string? status = esDifusion
            ? await _repositorio.GetStatusDifusionAsync(id, "SMS")
            : await _repositorio.GetStatusAsync(id);

        if (status == null)
        {
            _logger.LogWarning("El envío de {Origen} {Id} no existe; se descarta el mensaje.", origen, id);
            return;
        }

        if (status != "ENC")
        {
            _logger.LogWarning("El envío de {Origen} {Id} ya está en estatus '{Status}'; no se reenvía.", origen, id, status);
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
            _logger.LogError(ex, "Error al contactar al proveedor de SMS para el envío de {Origen} {Id}.", origen, id);

            // En una difusión, el último intento deja el envío en error para que
            // el avance no se quede esperando un mensaje que ya no se procesará.
            if (esDifusion && Reintentos.EsUltimoIntento(context))
            {
                await _repositorio.RegistrarEnvioDifusionAsync(id, "SMS", "ERROR");
                return;
            }
            throw;
        }

        // El proveedor respondió: el resultado es definitivo, no se reintenta.
        if (esDifusion)
        {
            await _repositorio.RegistrarEnvioDifusionAsync(id, "SMS", enviado ? "OK" : "ERROR");
        }
        else
        {
            await _repositorio.RegistrarEnvioAsync(id, enviado ? "OK" : "ERROR");
        }
    }
}
