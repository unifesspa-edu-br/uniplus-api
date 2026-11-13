namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A API UniPlus com Wolverine, Postgres e MinIO, com o limiar de pendência dos atos configurado com um resto de ticks
/// abaixo do microssegundo e o relógio <see cref="RelogioControlado"/>, que o teste leva a instantes com a 7ª casa.
/// </summary>
public sealed class InstantesEmResiduoApiFactory(string connectionString, MinioContainerFixture minio)
    : MonolitoApiFactory(connectionString, wolverineEnabled: true)
{
    public const string LimiarDePendenciaConfigurado = "00:30:00.0000005";

    internal RelogioControlado Relogio { get; } = new();

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        new("Storage:Endpoint", minio.Endpoint),
        new("Storage:AccessKey", MinioContainerFixture.AccessKey),
        new("Storage:SecretKey", MinioContainerFixture.SecretKey),
        new("Storage:BucketName", "uniplus-documentos-instantes"),
        new("AcervoPublico:Bucket", MinioContainerFixture.BucketDoAcervoPublico),
        new("AcervoPublico:EnderecoBase", minio.EnderecoDoAcervoPublico),
        new("Selecao:AtosDoProcesso:LimiarDePendencia", LimiarDePendenciaConfigurado),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AdicionarAdministradorDeProcessosDeTeste();
            services.AdicionarRegistroDeDecisoesDeTeste();
            services.RemoveAll<IAcervoPublico>();
            services.AddSingleton<IAcervoPublico, AcervoPublicoEmMemoria>();
            services.AddSingleton(Relogio);
            services.AddSingleton<TimeProvider>(Relogio);
        });
    }
}
