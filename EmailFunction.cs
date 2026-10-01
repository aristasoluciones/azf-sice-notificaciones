using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using sice.Functions.Notificaciones.Models;
using sice.Functions.Notificaciones.Services;


namespace sice.Functions.Notificaciones;
public class EmailFunction
{
    private readonly IEmailService _emailService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _config;
    private readonly INotificacionRepositorio _repositorio;
    private readonly ILogger<EmailFunction> _logger;

    public EmailFunction(IEmailService emailService, BlobServiceClient blobServiceClient, IConfiguration config, INotificacionRepositorio repositorio, ILogger<EmailFunction> logger)
    {
        _emailService = emailService;
        _blobServiceClient = blobServiceClient;
        _config = config;
        _repositorio = repositorio;
        _logger = logger;
    }

    [Function("ProcesarCorreo")]
    public async Task Run([QueueTrigger("%Queue:NombreCola%", Connection = "StorageNegocioConnection")] EmailQueueMessage datos, FunctionContext context)
    {
        // Los correos de difusión llevan su renglón: solo se envía lo que sigue
        // en cola (evita duplicados si la cola reprocesa el mensaje y respeta
        // una difusión cancelada) y se registra el resultado en la base.
        if (datos.IdDifusionDetalle.HasValue)
        {
            await ProcesarDifusion(datos, datos.IdDifusionDetalle.Value, context);
            return;
        }

        Stream? streamAdjunto = null;

        try
        {
            // --- Fase 1: Descarga del adjunto desde Blob Storage ---
            if (!string.IsNullOrEmpty(datos.NombreBlobAdjunto))
            {
                try
                {
                    var contenedorTemporal = _config["BlobStorage:ContenedorTemporal"];
                    var container = _blobServiceClient.GetBlobContainerClient(contenedorTemporal);
                    var blob = container.GetBlobClient(datos.NombreBlobAdjunto);
                    streamAdjunto = new MemoryStream();
                    await blob.DownloadToAsync(streamAdjunto);
                    streamAdjunto.Position = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al descargar el adjunto '{NombreBlob}' desde Blob Storage.", datos.NombreBlobAdjunto);
                    throw;
                }
            }

            // --- Fase 2: Envío del correo ---
            try
            {
                await _emailService.EnviarEmailAsync(datos, streamAdjunto!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar el correo a '{Destinatario}' con asunto '{Asunto}'.", datos.Destinatario, datos.Asunto);
                throw;
            }
        }
        finally
        {
            if (streamAdjunto != null)
            {
                await streamAdjunto.DisposeAsync();
            }
        }
    }

    private async Task ProcesarDifusion(EmailQueueMessage datos, int idDetalle, FunctionContext context)
    {
        string? status = await _repositorio.GetStatusDifusionAsync(idDetalle, "EMAIL");

        if (status == null)
        {
            _logger.LogWarning("El correo de difusión {Id} no existe; se descarta el mensaje.", idDetalle);
            return;
        }

        if (status != "ENC")
        {
            _logger.LogWarning("El correo de difusión {Id} ya está en estatus '{Status}'; no se reenvía.", idDetalle, status);
            return;
        }

        try
        {
            await _emailService.EnviarEmailAsync(datos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar el correo de difusión {Id}.", idDetalle);

            // Último intento: el envío queda en error en lugar de quedarse en cola.
            if (Reintentos.EsUltimoIntento(context))
            {
                await _repositorio.RegistrarEnvioDifusionAsync(idDetalle, "EMAIL", "ERROR");
                return;
            }
            throw;
        }

        await _repositorio.RegistrarEnvioDifusionAsync(idDetalle, "EMAIL", "OK");
    }
}