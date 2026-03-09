using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using sice.Functions.Notificaciones.Models;
using sice.Functions.Notificaciones.Services;


namespace sice.Functions.Notificaciones;
public class EmailFunction
{
    private readonly IEmailService _emailService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<EmailFunction> _logger;

    public EmailFunction(IEmailService emailService, BlobServiceClient blobServiceClient, ILogger<EmailFunction> logger)
    {
        _emailService = emailService;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    [Function("ProcesarCorreo")]
    public async Task Run([QueueTrigger("colacorreossice", Connection = "StorageNegocioConnection")] EmailQueueMessage datos)
    {
        Stream? streamAdjunto = null;

        try
        {
            // --- Fase 1: Descarga del adjunto desde Blob Storage ---
            if (!string.IsNullOrEmpty(datos.NombreBlobAdjunto))
            {
                try
                {
                    var container = _blobServiceClient.GetBlobContainerClient("contenedortmpsice");
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
}