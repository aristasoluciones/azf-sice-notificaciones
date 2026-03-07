using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Azure.Storage.Blobs;
using sice.Functions.Notificaciones.Models;
using sice.Functions.Notificaciones.Services;


namespace sice.Functions.Notificaciones;
public class EmailFunction
{
    private readonly IEmailService _emailService;
    private readonly BlobServiceClient _blobServiceClient;

    public EmailFunction(IEmailService emailService, BlobServiceClient blobServiceClient)
    {
        _emailService = emailService;
        _blobServiceClient = blobServiceClient;
    }

    [Function("ProcesarCorreo")]
    public async Task Run([QueueTrigger("colacorreossice", Connection = "StorageNegocioConnection")] EmailQueueMessage datos)
    {
       Stream? streamAdjunto = null;
       if(!string.IsNullOrEmpty(datos.NombreBlobAdjunto))
       {
            var container = _blobServiceClient.GetBlobContainerClient("contenedortmpsice");
            var blob = container.GetBlobClient(datos.NombreBlobAdjunto);
            streamAdjunto = new MemoryStream();
            await blob.DownloadToAsync(streamAdjunto);
            streamAdjunto.Position = 0; // Reiniciar la posición del stream después de la descarga
       }
       await _emailService.EnviarEmailAsync(datos, streamAdjunto!);
    }
}