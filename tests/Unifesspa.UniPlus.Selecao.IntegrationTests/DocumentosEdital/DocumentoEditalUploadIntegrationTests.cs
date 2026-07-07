namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosEdital;

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
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Integração real (Testcontainers Postgres + MinIO) do fluxo completo do
/// upload direto do documento do Edital (Story #759, T3 #784): iniciar → PUT
/// pre-assinado direto ao MinIO → confirmar → hash. Sem passar pelo HTTP
/// pipeline (controllers/auth/idempotência) — exercita Application +
/// Infrastructure reais, que é onde a integração com Postgres/MinIO importa.
/// </summary>
public sealed class DocumentoEditalUploadIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-test";
    private static readonly byte[] ConteudoPdfValido = [.. "%PDF-1.7 conteúdo de teste — Uni+ #784"u8];

    private readonly ProcessoSeletivoDbFixture _dbFixture;

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Intencional: o teste exercita o port da Application (IDocumentoEditalStorage), não o tipo concreto de Infrastructure — é o mesmo contrato que os handlers reais consomem.")]
    private readonly IDocumentoEditalStorage _storage;

    public DocumentoEditalUploadIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
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
        IStorageService storageService = provider.GetRequiredService<IStorageService>();
        IOptions<StorageOptions> options = provider.GetRequiredService<IOptions<StorageOptions>>();
        _storage = new DocumentoEditalStorageService(storageService, options);
    }

    private async Task<(SelecaoDbContext Context, ProcessoSeletivo Processo)> NovoProcessoAsync()
    {
        SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU (teste #784)", TipoProcesso.SiSU);
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return (context, processo);
    }

    [Fact(DisplayName = "Fluxo completo: iniciar → PUT pre-assinado → confirmar → hash bate com sha256 local")]
    public async Task FluxoCompleto_IniciarUploadConfirmar_HashConfere()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> iniciarResultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoEditalDto> confirmarResultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsSuccess.Should().BeTrue();
        confirmarResultado.Value!.Status.Should().Be("Confirmado");
        confirmarResultado.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);
        confirmarResultado.Value.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(ConteudoPdfValido)));
    }

    [Fact(DisplayName = "Sobrescrever a object key original após confirmação não afeta o conteúdo selado (P1)")]
    public async Task Confirmar_ObjectKeyOriginalSobrescritaDepois_ConteudoSeladoPermaneceIntacto()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> iniciarResultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        using HttpClient http = new();
        using ByteArrayContent conteudoOriginal = new(ConteudoPdfValido);
        conteudoOriginal.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putOriginal = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudoOriginal);
        putOriginal.EnsureSuccessStatusCode();

        Result<DocumentoEditalDto> confirmarResultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        confirmarResultado.IsSuccess.Should().BeTrue();

        DocumentoEdital documentoConfirmado = (await documentoRepository.ObterPorIdAsync(iniciarResultado.Value.DocumentoEditalId, CancellationToken.None))!;

        // A URL de upload original ainda vale (TTL não expirou) — um titular
        // mal-intencionado ou um retry duplicado do cliente sobrescreve a
        // ObjectKey de staging depois da confirmação.
        byte[] conteudoTrocado = [.. "%PDF-1.7 conteúdo trocado após a confirmação"u8];
        using ByteArrayContent conteudoSobrescrita = new(conteudoTrocado);
        conteudoSobrescrita.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putSobrescrita = await http.PutAsync(iniciarResultado.Value.UrlUpload, conteudoSobrescrita);
        putSobrescrita.EnsureSuccessStatusCode();

        await using Stream streamSelado = await _storage.AbrirLeituraAsync(
            documentoConfirmado.ObjectKeyConfirmado!, DocumentoEdital.TamanhoMaximoBytes + 1, CancellationToken.None);
        using MemoryStream buffer = new();
        await streamSelado.CopyToAsync(buffer, CancellationToken.None);

        buffer.ToArray().Should().Equal(ConteudoPdfValido);
        Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray())).Should().Be(confirmarResultado.Value!.HashSha256);
    }

    [Fact(DisplayName = "Confirmar sem upload prévio recusa (422 ObjetoNaoEncontrado)")]
    public async Task Confirmar_SemUploadPrevio_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> iniciarResultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        // Sem PUT ao MinIO — confirma direto, sem que o objeto exista.
        Result<DocumentoEditalDto> confirmarResultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value!.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsFailure.Should().BeTrue();
        confirmarResultado.Error!.Code.Should().Be("DocumentoEdital.ObjetoNaoEncontrado");
    }

    [Fact(DisplayName = "Confirmar objeto sem assinatura PDF válida recusa (422 AssinaturaInvalida)")]
    public async Task Confirmar_ObjetoSemAssinaturaPdf_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> iniciarResultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        byte[] conteudoFalso = [.. "não é um pdf de verdade"u8];
        using HttpClient http = new();
        using ByteArrayContent conteudo = new(conteudoFalso);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoEditalDto> confirmarResultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsFailure.Should().BeTrue();
        confirmarResultado.Error!.Code.Should().Be("DocumentoEdital.AssinaturaInvalida");
    }

    [Fact(DisplayName = "Confirmar um documento já confirmado recusa (imutabilidade — CA)")]
    public async Task Confirmar_DocumentoJaConfirmado_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> iniciarResultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoEditalDto> primeiraConfirmacao = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        primeiraConfirmacao.IsSuccess.Should().BeTrue();

        Result<DocumentoEditalDto> segundaConfirmacao = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processo.Id, iniciarResultado.Value.DocumentoEditalId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        segundaConfirmacao.IsFailure.Should().BeTrue();
        segundaConfirmacao.Error!.Code.Should().Be("DocumentoEdital.StatusInvalidoParaConfirmacao");
    }

    [Fact(DisplayName = "Iniciar upload duas vezes para o mesmo processo cria dois registros com object keys distintas (CA — nunca sobrescreve)")]
    public async Task IniciarUpload_DuasVezes_CriaRegistrosDistintos()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarUploadDocumentoEditalDto> primeiro = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        Result<IniciarUploadDocumentoEditalDto> segundo = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        primeiro.Value!.DocumentoEditalId.Should().NotBe(segundo.Value!.DocumentoEditalId);

        DocumentoEdital? doc1 = await documentoRepository.ObterPorIdAsync(primeiro.Value.DocumentoEditalId, CancellationToken.None);
        DocumentoEdital? doc2 = await documentoRepository.ObterPorIdAsync(segundo.Value.DocumentoEditalId, CancellationToken.None);
        doc1!.ObjectKey.Should().NotBe(doc2!.ObjectKey);
    }

    [Fact(DisplayName = "TentarReivindicarConfirmacaoAsync é atômico: só a primeira chamada ganha (Postgres real)")]
    public async Task TentarReivindicarConfirmacaoAsync_SoAPrimeiraChamadaGanha()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoEditalRepository documentoRepository = new(context);
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        context.DocumentosEdital.Add(documento);
        await context.SaveChangesAsync();

        bool primeira = await documentoRepository.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);
        bool segunda = await documentoRepository.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);

        primeira.Should().BeTrue();
        segunda.Should().BeFalse();
    }
}
