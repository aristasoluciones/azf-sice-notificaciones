using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using sice.Functions.Notificaciones.Services;
using System.Net.Security;

// Legacy compatibilidad de store procedures con Npgsql 6 (igual que el panel)
AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(sp => {
    // builder.Configuration ya tiene acceso a tus variables de entorno/local.settings.json
    string connectionString = builder.Configuration["AzureWebJobsStorage"]!;
    return new BlobServiceClient(connectionString);
});
builder.Services.AddScoped<IEmailService, EmailService>();

// Cliente del proveedor de SMS. Su certificado está emitido a nombre de otro
// host del mismo servidor, así que la conexión falla por desajuste de nombre.
// Mientras el proveedor no corrija el certificado, se tolera ESE fallo y solo
// si el certificado corresponde al nombre configurado en Sms:CertificadoNombre.
// La cadena la sigue validando la autoridad certificadora: un certificado
// caducado, revocado o de otro emisor se rechaza igual que siempre.
// Con Sms:CertificadoNombre vacío, la validación es la estándar.
builder.Services.AddHttpClient(SmsService.ClienteHttp)
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var handler = new HttpClientHandler();
        string? nombreEsperado = builder.Configuration["Sms:CertificadoNombre"];

        if (!string.IsNullOrWhiteSpace(nombreEsperado))
        {
            handler.ServerCertificateCustomValidationCallback = (_, cert, _, errores) =>
            {
                if (errores == SslPolicyErrors.None) { return true; }
                if (errores != SslPolicyErrors.RemoteCertificateNameMismatch) { return false; }

                return cert != null && cert.MatchesHostname(nombreEsperado);
            };
        }

        return handler;
    });

builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<INotificacionRepositorio, NotificacionRepositorio>();

builder.Build().Run();
