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
///
/// Regla: un SMS que ya salió nunca vuelve a la cola. Solo se deja reintentar a
/// la cola cuando todavía no se envió nada (no se pudo leer el estatus o el
/// proveedor no respondió).
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
        // enviado, el estatus lo delata y aquí se detiene. Si la base no
        // responde ni con reintentos, la excepción devuelve el mensaje a la
        // cola: todavía no se envió nada, así que reintentar es seguro.
        string? status = await Reintentos.BaseDatosAsync(
            () => esDifusion ? _repositorio.GetStatusDifusionAsync(id, "SMS") : _repositorio.GetStatusAsync(id),
            _logger, $"leer el estatus del envío de {origen} {id}");

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
            // Falla de comunicación con el proveedor: se deja que la cola
            // reintente y el estatus sigue en 'ENC'.
            _logger.LogError(ex, "Error al contactar al proveedor de SMS para el envío de {Origen} {Id}.", origen, id);

            // En una difusión, el último intento deja el envío en error para que
            // el avance no se quede esperando un mensaje que ya no se procesará.
            if (esDifusion && Reintentos.EsUltimoIntento(context))
            {
                await Reintentos.RegistrarResultadoAsync(
                    () => _repositorio.RegistrarEnvioDifusionAsync(id, "SMS", "ERROR"), _logger, origen, id, "ERROR");
                return;
            }
            throw;
        }

        // El proveedor respondió: el resultado es definitivo y el mensaje no
        // vuelve a la cola aunque la base no responda al registrarlo.
        string resultado = enviado ? "OK" : "ERROR";
        await Reintentos.RegistrarResultadoAsync(
            () => esDifusion ? _repositorio.RegistrarEnvioDifusionAsync(id, "SMS", resultado) : _repositorio.RegistrarEnvioAsync(id, resultado),
            _logger, origen, id, resultado);
    }
}
