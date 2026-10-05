using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace sice.Functions.Notificaciones;

/// <summary>
/// Reintentos de la cola sobre un mensaje y reintentos locales de las
/// operaciones de base de datos.
/// </summary>
public static class Reintentos
{
    // Debe coincidir con extensions:queues:maxDequeueCount de host.json.
    private const int MaximoIntentos = 3;

    // Esperas entre reintentos locales de base de datos (el servidor puede quedarse
    // sin conexiones disponibles por unos segundos).
    private static readonly TimeSpan[] Esperas = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Último intento de la cola sobre el mensaje. Sirve para dar por fallido un
    /// envío de difusión en lugar de dejarlo "en cola" para siempre cuando el
    /// mensaje se va a la cola de mensajes fallidos.
    /// </summary>
    public static bool EsUltimoIntento(FunctionContext context)
    {
        if (context.BindingContext.BindingData.TryGetValue("DequeueCount", out object? valor) &&
            long.TryParse(valor?.ToString(), out long intento))
        {
            return intento >= MaximoIntentos;
        }
        return false;
    }

    /// <summary>
    /// Ejecuta una operación de base de datos reintentándola localmente con espera.
    /// Si después de los reintentos sigue fallando, lanza la última excepción.
    /// </summary>
    public static async Task<T> BaseDatosAsync<T>(Func<Task<T>> operacion, ILogger logger, string descripcion)
    {
        for (int i = 0; ; i++)
        {
            try
            {
                return await operacion();
            }
            catch (Exception ex) when (i < Esperas.Length)
            {
                logger.LogWarning(ex, "Falló {Descripcion} (intento {Intento}); se reintenta en {Segundos} s.",
                    descripcion, i + 1, Esperas[i].TotalSeconds);
                await Task.Delay(Esperas[i]);
            }
        }
    }

    public static Task BaseDatosAsync(Func<Task> operacion, ILogger logger, string descripcion)
    {
        return BaseDatosAsync(async () => { await operacion(); return true; }, logger, descripcion);
    }

    /// <summary>
    /// Registra el resultado de un envío que YA salió. Nunca lanza excepción: si
    /// la base no responde ni con los reintentos, se deja constancia en el log y
    /// el mensaje no vuelve a la cola, porque reintentarlo lo enviaría otra vez
    /// (un SMS cobrado dos veces o un correo duplicado).
    /// </summary>
    public static async Task RegistrarResultadoAsync(Func<Task> registrar, ILogger logger, string origen, int id, string status)
    {
        try
        {
            await BaseDatosAsync(registrar, logger, $"registrar el resultado del envío de {origen} {id}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ENVÍO SIN REGISTRAR: el envío de {Origen} {Id} salió con resultado {Status}, pero no se pudo guardar en la base. Queda en 'ENC'; no se reintenta para no duplicarlo.",
                origen, id, status);
        }
    }
}
