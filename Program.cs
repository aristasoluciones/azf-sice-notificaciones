using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using sice.Functions.Notificaciones.Services;

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
builder.Build().Run();
