namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Testcontainers Postgres + MinIO) do envio do modelo de documento: iniciar →
/// PUT pré-assinado direto ao MinIO com o content-type do formato → confirmar → a URL de acesso
/// abre a cópia selada, com o mesmo hash.
/// </summary>
public sealed class ModeloDeDocumentoEnvioIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-test";

    private readonly ProcessoSeletivoDbFixture _dbFixture;

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Intencional: o teste exercita o port da Application, o mesmo contrato que os handlers consomem.")]
    private readonly IArquivoArmazenadoStorage _storage;

    public ModeloDeDocumentoEnvioIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        ArgumentNullException.ThrowIfNull(minio);
        _dbFixture = dbFixture;

        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = TestBucket,
        };
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        ServiceProvider provider = services.BuildServiceProvider();
        _storage = new ArquivoArmazenadoStorageService(
            provider.GetRequiredService<IStorageService>(), provider.GetRequiredService<IOptions<StorageOptions>>());
    }

    [Fact(DisplayName = "Fluxo completo: o ODT enviado é confirmado com o hash local, e a URL de acesso abre a cópia selada")]
    public async Task FluxoCompleto_OdtConfirmado_AcessoAbreACopiaSelada()
    {
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2027 — modelo de documento", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        ModeloDeDocumentoRepository modelos = new(context);
        ProcessoSeletivoRepository processos = new(context, TimeProvider.System);

        Result<IniciarEnvioDoModeloDeDocumentoDto> iniciar = await IniciarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new IniciarEnvioDoModeloDeDocumentoCommand(processo.Id, "Declaração de pertencimento quilombola", "ODT"),
            processos, modelos, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        using HttpClient http = new();
        using ByteArrayContent corpo = new(odt);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(iniciar.Value!.ContentTypeExigido);
        (await http.PutAsync(iniciar.Value.UrlUpload, corpo)).EnsureSuccessStatusCode();

        Result<ModeloDeDocumentoDto> confirmar = await ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new ConfirmarEnvioDoModeloDeDocumentoCommand(processo.Id, iniciar.Value.ModeloDeDocumentoId),
            modelos, _storage, context, TimeProvider.System, CancellationToken.None);
        confirmar.IsSuccess.Should().BeTrue(confirmar.Error?.Message);
        confirmar.Value!.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(odt)));
        confirmar.Value.NomeArquivo.Should().Be("Declaração de pertencimento quilombola.odt");

        Result<AcessoModeloDeDocumentoDto> acesso = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            new ObterAcessoModeloDeDocumentoQuery(processo.Id, iniciar.Value.ModeloDeDocumentoId),
            processos, modelos, _storage, TimeProvider.System, CancellationToken.None);
        acesso.IsSuccess.Should().BeTrue(acesso.Error?.Message);
        byte[] baixado = await http.GetByteArrayAsync(acesso.Value!.Url);
        baixado.Should().Equal(odt);
    }
}
