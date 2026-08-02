using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sice.Functions.Notificaciones.Models;
using System.Text.Json;

namespace sice.Functions.Notificaciones.Services
{
    /// <summary>
    /// Único punto del sistema que conoce las credenciales del proveedor de SMS.
    /// Se leen de la configuración de la aplicación de funciones; nunca del
    /// repositorio ni del navegador.
    /// </summary>
    public class SmsService : ISmsService
    {
        /// <summary>Cliente HTTP con la validación de certificado del proveedor (ver Program).</summary>
        public const string ClienteHttp = "sms";

        private readonly IConfiguration _config;
        private readonly ILogger<SmsService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public SmsService(IConfiguration config, ILogger<SmsService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> EnviarSmsAsync(SmsQueueMessage datos)
        {
            string urlApi = _config["Sms:UrlApi"]!;
            string apiKey = _config["Sms:ApiKey"]!;
            string clientId = _config["Sms:ClientId"]!;
            string senderId = _config["Sms:SenderId"]!;

            // Las credenciales viajan tal como las espera el proveedor; solo se
            // codifican el texto y el número, que son los datos variables.
            string url = $"{urlApi}?ApiKey={apiKey}&ClientId={clientId}&SenderId={senderId}" +
                         $"&Is_Unicode=false&Is_Flash=false" +
                         $"&Message={Uri.EscapeDataString(datos.Mensaje)}" +
                         $"&MobileNumbers={Uri.EscapeDataString(datos.Destinatario)}";

            var httpClient = _httpClientFactory.CreateClient(ClienteHttp);
            var respuesta = await httpClient.GetAsync(url);

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogError("El proveedor de SMS respondió {Codigo} para la notificación {Id}.",
                    (int)respuesta.StatusCode, datos.IdNotificacion);
                return false;
            }

            string contenido = await respuesta.Content.ReadAsStringAsync();
            SmsProveedorRespuesta? resultado;
            try
            {
                resultado = JsonSerializer.Deserialize<SmsProveedorRespuesta>(contenido,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "No se pudo interpretar la respuesta del proveedor de SMS para la notificación {Id}.",
                    datos.IdNotificacion);
                return false;
            }

            if (resultado == null || resultado.ErrorCode != 0)
            {
                _logger.LogError("El proveedor de SMS rechazó la notificación {Id}: {Error}",
                    datos.IdNotificacion, resultado?.ErrorDescription ?? "sin detalle");
                return false;
            }

            return true;
        }
    }
}
