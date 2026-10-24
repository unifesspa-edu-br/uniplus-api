using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Minio;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Observabilidade;

using Wolverine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonConsole();

VerificadorOptions opcoes = builder.Configuration.GetSection(VerificadorOptions.SectionName).Get<VerificadorOptions>() ?? new VerificadorOptions();
ValidateOptionsResult validacao = new ValidadorDasOpcoes().Validate(Options.DefaultName, opcoes);
if (validacao.Failed)
{
    throw new OptionsValidationException(Options.DefaultName, typeof(VerificadorOptions), validacao.Failures);
}

builder.Services.AddSingleton(Options.Create(opcoes));

// Registrado por fábrica: o contêiner descarta o cliente do MinIO ao encerrar.
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    ArmazenamentoOptions armazenamento = sp.GetRequiredService<IOptions<VerificadorOptions>>().Value.Armazenamento;
    return new MinioClient()
        .WithEndpoint(armazenamento.Endpoint)
        .WithCredentials(armazenamento.AccessKey, armazenamento.SecretKey)
        .WithSSL(armazenamento.UsarSsl)
        .Build();
});
builder.Services.AddSingleton<IArmazenamentoDeArquivos, ArmazenamentoMinio>();
builder.Services.AddSingleton<IClamd, ClamdClient>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MetricasDoVerificador>();
builder.Services.AddHostedService<IdadeDaBaseWorker>();

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(recurso => recurso.AddService("uniplus-verificador", serviceNamespace: "uniplus"))
        .WithMetrics(metricas => metricas
            .AddMeter(MetricasDoVerificador.NomeDoMedidor)
            .AddMeter("Wolverine")
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());
}

builder.Services.AddWolverine(wolverine => ConfiguracaoDoWolverine.Configurar(wolverine, opcoes));

await builder.Build().RunAsync().ConfigureAwait(false);
