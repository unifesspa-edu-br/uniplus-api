namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Testcontainers Postgres + MinIO) do fluxo completo do
/// envio direto do documento a publicar: iniciar → PUT pre-assinado direto ao
/// MinIO → confirmar → hash. Sem passar pelo HTTP
/// pipeline (controllers/auth/idempotência) — exercita Application +
/// Infrastructure reais, que é onde a integração com Postgres/MinIO importa.
/// </summary>
public sealed class DocumentoAPublicarEnvioIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-test";
    private static readonly byte[] ConteudoPdfValido = [.. "%PDF-1.7 conteúdo de teste — Uni+ #784"u8];

    private readonly ProcessoSeletivoDbFixture _dbFixture;

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Intencional: o teste exercita o port da Application (IArquivoArmazenadoStorage), não o tipo concreto de Infrastructure — é o mesmo contrato que os handlers reais consomem.")]
    private readonly IArquivoArmazenadoStorage _storage;

    public DocumentoAPublicarEnvioIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
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
        _storage = new ArquivoArmazenadoStorageService(storageService, options);
    }

    private async Task<(SelecaoDbContext Context, ProcessoSeletivo Processo)> NovoProcessoAsync()
    {
        SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU (teste #784)", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot
                .Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return (context, processo);
    }

    [Fact(DisplayName = "Fluxo completo: iniciar → PUT pre-assinado → confirmar → hash bate com sha256 local")]
    public async Task FluxoCompleto_IniciarEnvioConfirmar_HashConfere()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmarResultado = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsSuccess.Should().BeTrue();
        confirmarResultado.Value!.Status.Should().Be("Confirmado");
        confirmarResultado.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);
        confirmarResultado.Value.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(ConteudoPdfValido)));
    }

    [Theory(DisplayName = "DOCX e ODT seguem o mesmo fluxo: o conteúdo selado tem o hash do arquivo e o tipo de mídia do formato")]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task FluxoCompleto_DocumentoDeTexto_HashConfereESelaComOTipoDeMidia(string tipoDeMidia)
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);
        byte[] arquivo = tipoDeMidia == TiposDeMidiaDeDocumento.Docx ? ArquivosDeModeloDeTeste.Docx() : ArquivosDeModeloDeTeste.Odt();

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.AnexoDoEdital, "Anexo I", tipoDeMidia),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue();
        iniciar.Value!.ContentTypeExigido.Should().Be(tipoDeMidia);

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipoDeMidia);
        (await http.PutAsync(iniciar.Value.UrlUpload, conteudo)).EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmar = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciar.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmar.IsSuccess.Should().BeTrue();
        confirmar.Value!.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(arquivo)));
        confirmar.Value.TipoDeMidia.Should().Be(tipoDeMidia);
        DocumentoAPublicar gravado = await context.DocumentosAPublicar.AsNoTracking().SingleAsync(d => d.Id == iniciar.Value.DocumentoAPublicarId);
        InfoObjetoArmazenado? selado = await _storage.ObterInfoAsync(gravado.ObjectKeyConfirmado!);
        selado.Should().NotBeNull();
        selado!.ContentType.Should().Be(tipoDeMidia);
    }

    /// <summary>
    /// O que nenhum teste de unidade alcança: a URL emitida abre de fato o
    /// objeto no storage. Um handler que devolvesse uma URL bem formada e
    /// inservível — chave errada, assinatura de outro método, bucket trocado —
    /// passaria em qualquer mock e falharia no primeiro clique do operador.
    /// </summary>
    [Fact(DisplayName = "Acesso a documento confirmado devolve URL que abre o conteúdo selado")]
    public async Task Acesso_DocumentoConfirmado_UrlAbreConteudoSelado()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue();

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        (await http.PutAsync(iniciar.Value!.UrlUpload, conteudo)).EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmar = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciar.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        confirmar.IsSuccess.Should().BeTrue();

        Result<AcessoDocumentoAPublicarDto> acesso = await ObterAcessoDocumentoAPublicarQueryHandler.Handle(
            new ObterAcessoDocumentoAPublicarQuery(processo.Id, iniciar.Value.DocumentoAPublicarId),
            processoRepository, documentoRepository, _storage, TimeProvider.System, CancellationToken.None);

        acesso.IsSuccess.Should().BeTrue();

        HttpResponseMessage leitura = await http.GetAsync(acesso.Value!.Url);
        leitura.EnsureSuccessStatusCode();
        byte[] baixado = await leitura.Content.ReadAsByteArrayAsync();
        baixado.Should().Equal(ConteudoPdfValido);
    }

    [Fact(DisplayName = "Acesso a documento pendente recusa — o conteúdo ainda não foi validado")]
    public async Task Acesso_DocumentoPendente_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue();

        Result<AcessoDocumentoAPublicarDto> acesso = await ObterAcessoDocumentoAPublicarQueryHandler.Handle(
            new ObterAcessoDocumentoAPublicarQuery(processo.Id, iniciar.Value!.DocumentoAPublicarId),
            processoRepository, documentoRepository, _storage, TimeProvider.System, CancellationToken.None);

        acesso.IsFailure.Should().BeTrue();
        acesso.Error!.Code.Should().Be("DocumentoAPublicar.NaoConfirmado");
    }

    [Fact(DisplayName = "Sobrescrever a object key original após confirmação não afeta o conteúdo selado (P1)")]
    public async Task Confirmar_ObjectKeyOriginalSobrescritaDepois_ConteudoSeladoPermaneceIntacto()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        using HttpClient http = new();
        using ByteArrayContent conteudoOriginal = new(ConteudoPdfValido);
        conteudoOriginal.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putOriginal = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudoOriginal);
        putOriginal.EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmarResultado = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        confirmarResultado.IsSuccess.Should().BeTrue();

        DocumentoAPublicar documentoConfirmado = (await documentoRepository.ObterPorIdAsync(iniciarResultado.Value.DocumentoAPublicarId, CancellationToken.None))!;

        // A URL de upload original ainda vale (TTL não expirou) — um titular
        // mal-intencionado ou um retry duplicado do cliente sobrescreve a
        // ObjectKey de staging depois da confirmação.
        byte[] conteudoTrocado = [.. "%PDF-1.7 conteúdo trocado após a confirmação"u8];
        using ByteArrayContent conteudoSobrescrita = new(conteudoTrocado);
        conteudoSobrescrita.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putSobrescrita = await http.PutAsync(iniciarResultado.Value.UrlUpload, conteudoSobrescrita);
        putSobrescrita.EnsureSuccessStatusCode();

        await using Stream streamSelado = await _storage.AbrirLeituraAsync(
            documentoConfirmado.ObjectKeyConfirmado!, DocumentoAPublicar.TamanhoMaximoBytes + 1, CancellationToken.None);
        using MemoryStream buffer = new();
        await streamSelado.CopyToAsync(buffer, CancellationToken.None);

        buffer.ToArray().Should().Equal(ConteudoPdfValido);
        Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray())).Should().Be(confirmarResultado.Value!.HashSha256);
    }

    [Fact(DisplayName = "Confirmar sem upload prévio recusa (422 ObjetoNaoEncontrado)")]
    public async Task Confirmar_SemUploadPrevio_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        // Sem PUT ao MinIO — confirma direto, sem que o objeto exista.
        Result<DocumentoAPublicarDto> confirmarResultado = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value!.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsFailure.Should().BeTrue();
        confirmarResultado.Error!.Code.Should().Be("DocumentoAPublicar.ObjetoNaoEncontrado");
    }

    [Fact(DisplayName = "Confirmar objeto sem assinatura PDF válida recusa (422 ConteudoDivergeDoFormato)")]
    public async Task Confirmar_ObjetoSemAssinaturaPdf_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        byte[] conteudoFalso = [.. "não é um pdf de verdade"u8];
        using HttpClient http = new();
        using ByteArrayContent conteudo = new(conteudoFalso);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmarResultado = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsFailure.Should().BeTrue();
        confirmarResultado.Error!.Code.Should().Be("DocumentoAPublicar.ConteudoDivergeDoFormato");
    }

    [Fact(DisplayName = "Confirmar objeto de 0 bytes recusa (422 ArquivoVazio, não 500 — Range GET não é satisfazível sobre objeto vazio)")]
    public async Task Confirmar_ObjetoVazio_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        iniciarResultado.IsSuccess.Should().BeTrue();

        using HttpClient http = new();
        using ByteArrayContent conteudoVazio = new([]);
        conteudoVazio.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudoVazio);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> confirmarResultado = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        confirmarResultado.IsFailure.Should().BeTrue();
        confirmarResultado.Error!.Code.Should().Be("DocumentoAPublicar.ArquivoVazio");
    }

    [Fact(DisplayName = "Confirmar um documento já confirmado recusa (imutabilidade — CA)")]
    public async Task Confirmar_DocumentoJaConfirmado_Recusa()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciarResultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        HttpResponseMessage putResponse = await http.PutAsync(iniciarResultado.Value!.UrlUpload, conteudo);
        putResponse.EnsureSuccessStatusCode();

        Result<DocumentoAPublicarDto> primeiraConfirmacao = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        primeiraConfirmacao.IsSuccess.Should().BeTrue();

        Result<DocumentoAPublicarDto> segundaConfirmacao = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciarResultado.Value.DocumentoAPublicarId),
            documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        segundaConfirmacao.IsFailure.Should().BeTrue();
        segundaConfirmacao.Error!.Code.Should().Be("DocumentoAPublicar.StatusInvalidoParaConfirmacao");
    }

    [Fact(DisplayName = "Iniciar upload duas vezes para o mesmo processo cria dois registros com object keys distintas (CA — nunca sobrescreve)")]
    public async Task IniciarUpload_DuasVezes_CriaRegistrosDistintos()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        ProcessoSeletivoRepository processoRepository = new(context, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> primeiro = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);
        Result<IniciarEnvioDoDocumentoAPublicarDto> segundo = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, _storage, context, TimeProvider.System, CancellationToken.None);

        primeiro.Value!.DocumentoAPublicarId.Should().NotBe(segundo.Value!.DocumentoAPublicarId);

        DocumentoAPublicar? doc1 = await documentoRepository.ObterPorIdAsync(primeiro.Value.DocumentoAPublicarId, CancellationToken.None);
        DocumentoAPublicar? doc2 = await documentoRepository.ObterPorIdAsync(segundo.Value.DocumentoAPublicarId, CancellationToken.None);
        doc1!.ObjectKey.Should().NotBe(doc2!.ObjectKey);
    }

    [Fact(DisplayName = "TentarReivindicarConfirmacaoAsync é atômico: só a primeira chamada ganha (Postgres real)")]
    public async Task TentarReivindicarConfirmacaoAsync_SoAPrimeiraChamadaGanha()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();

        bool primeira = await documentoRepository.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);
        bool segunda = await documentoRepository.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);

        primeira.Should().BeTrue();
        segunda.Should().BeFalse();
    }

    [Fact(DisplayName = "A finalidade fica gravada e a listagem filtra por ela, sem misturar o anexo PDF com o edital")]
    public async Task Listar_FiltraPelaFinalidadeGravada()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicar edital = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        DocumentoAPublicar anexoPdf = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.AnexoDoEdital, "quadro.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        DocumentoAPublicar anexoDocx = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.AnexoDoEdital, "formulario.docx", TiposDeMidiaDeDocumento.Docx, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        context.DocumentosAPublicar.AddRange(edital, anexoPdf, anexoDocx);
        await context.SaveChangesAsync();
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        DocumentoAPublicarRepository repositorio = new(leitura);

        IReadOnlyList<DocumentoAPublicar> editais = await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital);
        IReadOnlyList<DocumentoAPublicar> anexos = await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.AnexoDoEdital);
        IReadOnlyList<DocumentoAPublicar> todos = await repositorio.ListarPorProcessoAsync(processo.Id, null);

        editais.Select(static d => d.Id).Should().Equal(edital.Id);
        anexos.Select(static d => d.Id).Should().BeEquivalentTo([anexoPdf.Id, anexoDocx.Id]);
        todos.Should().HaveCount(3);
        (await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.DocumentoDeAto)).Should().BeEmpty();
    }

    [Fact(DisplayName = "TentarReivindicarConfirmacaoAsync participa da transação ambiente: reverte em rollback")]
    public async Task TentarReivindicarConfirmacaoAsync_RevertNoRollbackDaTransacaoAmbiente()
    {
        (SelecaoDbContext context, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository documentoRepository = new(context);
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();

        // Simula o mesmo cenário do handler sob Wolverine (ADR-0004,
        // AutoApplyTransactions): a reivindicação roda dentro da transação
        // ambiente do comando. Se um passo posterior falhar (ex.: o storage
        // indisponível), o rollback dessa transação desfaz a reivindicação
        // junto — o documento volta a Pendente e a confirmação é retentável.
        await using (IDbContextTransaction transacao = await context.Database.BeginTransactionAsync())
        {
            bool reivindicou = await documentoRepository.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);
            reivindicou.Should().BeTrue();
            await transacao.RollbackAsync();
        }

        await using SelecaoDbContext contextoIndependente = _dbFixture.CreateDbContext();
        DocumentoAPublicar documentoAposRollback = await contextoIndependente.DocumentosAPublicar
            .FirstAsync(d => d.Id == documento.Id);
        documentoAposRollback.Status.Should().Be(StatusArquivoEnviado.Pendente);
    }

    [Fact(DisplayName = "Falha após reivindicação reverte para pendente e retry converge na mesma cópia selada")]
    public async Task Confirmar_FalhaAoSalvarCopiaSelada_ReverteEPermiteRetrySemOrfaos()
    {
        (SelecaoDbContext contextoInicial, ProcessoSeletivo processo) = await NovoProcessoAsync();
        DocumentoAPublicarRepository repositorioInicial = new(contextoInicial);
        ProcessoSeletivoRepository processoRepository = new(contextoInicial, TimeProvider.System);

        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, repositorioInicial, _storage, contextoInicial, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue();

        using HttpClient http = new();
        using ByteArrayContent conteudo = new(ConteudoPdfValido);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        (await http.PutAsync(iniciar.Value!.UrlUpload, conteudo)).EnsureSuccessStatusCode();

        FailingSealedStorage storageFalho = new(_storage);
        await using (IDbContextTransaction transacao = await contextoInicial.Database.BeginTransactionAsync())
        {
            Func<Task> act = () => ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
                new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciar.Value.DocumentoAPublicarId),
                repositorioInicial, storageFalho, contextoInicial, TimeProvider.System, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            await transacao.RollbackAsync();
        }

        await using SelecaoDbContext contextoAposFalha = _dbFixture.CreateDbContext();
        DocumentoAPublicar documentoAposFalha = await contextoAposFalha.DocumentosAPublicar
            .FirstAsync(d => d.Id == iniciar.Value.DocumentoAPublicarId);
        documentoAposFalha.Status.Should().Be(StatusArquivoEnviado.Pendente);
        documentoAposFalha.HashSha256.Should().BeNull();
        documentoAposFalha.ObjectKeyConfirmado.Should().BeNull();

        string objectKeySeladoEsperado =
            $"selecao/documentos-a-publicar/{processo.Id:D}/{documentoAposFalha.Id:D}/confirmado.pdf";
        (await _storage.ObterInfoAsync(objectKeySeladoEsperado, CancellationToken.None))
            .Should().NotBeNull();

        DocumentoAPublicarRepository repositorioRetry = new(contextoAposFalha);
        Result<DocumentoAPublicarDto> retry = await ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processo.Id, iniciar.Value.DocumentoAPublicarId),
            repositorioRetry, _storage, contextoAposFalha, TimeProvider.System, CancellationToken.None);

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.Status.Should().Be("Confirmado");
        retry.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);

        DocumentoAPublicar documentoConfirmado = await contextoAposFalha.DocumentosAPublicar
            .AsNoTracking()
            .FirstAsync(d => d.Id == iniciar.Value.DocumentoAPublicarId);
        documentoConfirmado.ObjectKeyConfirmado.Should().Be(objectKeySeladoEsperado);

        await using Stream streamSelado = await _storage.AbrirLeituraAsync(
            documentoConfirmado.ObjectKeyConfirmado!, DocumentoAPublicar.TamanhoMaximoBytes + 1, CancellationToken.None);
        using MemoryStream buffer = new();
        await streamSelado.CopyToAsync(buffer, CancellationToken.None);
        buffer.ToArray().Should().Equal(ConteudoPdfValido);
    }

    private sealed class FailingSealedStorage(IArquivoArmazenadoStorage inner) : IArquivoArmazenadoStorage
    {
        public Task<string> GerarUrlUploadAsync(string objectKey, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            inner.GerarUrlUploadAsync(objectKey, contentType, expiracao, cancellationToken);

        public Task<string> GerarUrlLeituraAsync(string objectKey, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            inner.GerarUrlLeituraAsync(objectKey, expiracao, cancellationToken);

        public Task<InfoObjetoArmazenado?> ObterInfoAsync(string objectKey, CancellationToken cancellationToken = default) =>
            inner.ObterInfoAsync(objectKey, cancellationToken);

        public Task<Stream> AbrirLeituraAsync(string objectKey, long limiteBytes, CancellationToken cancellationToken = default) =>
            inner.AbrirLeituraAsync(objectKey, limiteBytes, cancellationToken);

        public async Task SalvarConteudoSeladoAsync(string objectKey, byte[] conteudo, string contentType, CancellationToken cancellationToken = default)
        {
            await inner.SalvarConteudoSeladoAsync(objectKey, conteudo, contentType, cancellationToken);
            throw new InvalidOperationException("Falha simulada depois de salvar a cópia selada.");
        }

        public Task RemoverAsync(string objectKey, CancellationToken cancellationToken = default) =>
            inner.RemoverAsync(objectKey, cancellationToken);
    }
}
