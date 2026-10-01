using Microsoft.Azure.Functions.Worker;

namespace sice.Functions.Notificaciones;

/// <summary>
/// Intentos de la cola sobre un mensaje. Sirve para dar por fallido un envío de
/// difusión en su último intento, en lugar de dejarlo "en cola" para siempre
/// cuando el mensaje se va a la cola de mensajes fallidos.
/// </summary>
public static class Reintentos
{
    // Debe coincidir con extensions:queues:maxDequeueCount de host.json.
    private const int MaximoIntentos = 3;

    public static bool EsUltimoIntento(FunctionContext context)
    {
        if (context.BindingContext.BindingData.TryGetValue("DequeueCount", out object? valor) &&
            long.TryParse(valor?.ToString(), out long intento))
        {
            return intento >= MaximoIntentos;
        }
        return false;
    }
}
