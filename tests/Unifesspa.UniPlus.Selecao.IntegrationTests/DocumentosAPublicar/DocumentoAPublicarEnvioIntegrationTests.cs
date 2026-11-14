namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
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
    private readonly ArquivoArmazenadoStorageService _storage;

    public DocumentoAPublicarEnvioIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        _dbFixture = dbFixture;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
    }

    [Fact(DisplayName = "Fluxo completo: iniciar → PUT pre-assinado → confirmar → hash bate com sha256 local")]
    public async Task FluxoCompleto_IniciarEnvioConfirmar_HashConfere()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarEEnviarAsync(ConteudoPdfValido);

        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);

        confirmar.IsSuccess.Should().BeTrue();
        confirmar.Value!.Status.Should().Be("Confirmado");
        confirmar.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);
        confirmar.Value.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(ConteudoPdfValido)));
    }

    [Theory(DisplayName = "DOCX e ODT seguem o mesmo fluxo: o conteúdo selado tem o hash do arquivo e o tipo de mídia do formato")]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task FluxoCompleto_DocumentoDeTexto_HashConfereESelaComOTipoDeMidia(string tipoDeMidia)
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        byte[] arquivo = tipoDeMidia == TiposDeMidiaDeDocumento.Docx ? ArquivosDeModeloDeTeste.Docx() : ArquivosDeModeloDeTeste.Odt();
        IniciarEnvioDoDocumentoAPublicarDto iniciar =
            await envio.IniciarEEnviarAsync(arquivo, tipoDeMidia, FinalidadeDoDocumento.AnexoDoEdital, "Anexo I");

        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);

        iniciar.ContentTypeExigido.Should().Be(tipoDeMidia);
        confirmar.IsSuccess.Should().BeTrue();
        confirmar.Value!.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(arquivo)));
        confirmar.Value.TipoDeMidia.Should().Be(tipoDeMidia);
        await ConferirCopiaSeladaComOTipoDeMidiaAsync(envio, iniciar.DocumentoAPublicarId, tipoDeMidia);
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
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdfValido)).DocumentoAPublicarId;
        (await envio.ConfirmarAsync(documentoId)).IsSuccess.Should().BeTrue();

        Result<AcessoDocumentoAPublicarDto> acesso = await envio.AcessarAsync(documentoId);

        acesso.IsSuccess.Should().BeTrue();
        using HttpClient http = new();
        (await http.GetByteArrayAsync(acesso.Value!.Url)).Should().Equal(ConteudoPdfValido);
    }

    [Fact(DisplayName = "Acesso a documento pendente recusa — o conteúdo ainda não foi validado")]
    public async Task Acesso_DocumentoPendente_Recusa()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarAsync();

        Result<AcessoDocumentoAPublicarDto> acesso = await envio.AcessarAsync(iniciar.DocumentoAPublicarId);

        acesso.IsFailure.Should().BeTrue();
        acesso.Error!.Code.Should().Be("DocumentoAPublicar.NaoConfirmado");
    }

    /// <summary>
    /// A URL de upload original ainda vale (TTL não expirou): um titular mal-intencionado ou um retry duplicado do
    /// cliente sobrescreve a ObjectKey de staging depois da confirmação.
    /// </summary>
    [Fact(DisplayName = "Sobrescrever a object key original após confirmação não afeta o conteúdo selado (P1)")]
    public async Task Confirmar_ObjectKeyOriginalSobrescritaDepois_ConteudoSeladoPermaneceIntacto()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarEEnviarAsync(ConteudoPdfValido);
        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);
        confirmar.IsSuccess.Should().BeTrue();
        DocumentoAPublicar confirmado = (await envio.Documentos.ObterPorIdAsync(iniciar.DocumentoAPublicarId, CancellationToken.None))!;

        await EnvioDoDocumentoAPublicar.EnviarAsync(iniciar.UrlUpload, [.. "%PDF-1.7 conteúdo trocado após a confirmação"u8]);

        byte[] selado = await LerSeladoAsync(confirmado.ObjectKeyConfirmado!);
        selado.Should().Equal(ConteudoPdfValido);
        Convert.ToHexStringLower(SHA256.HashData(selado)).Should().Be(confirmar.Value!.HashSha256);
    }

    [Fact(DisplayName = "Confirmar sem upload prévio recusa (422 ObjetoNaoEncontrado)")]
    public async Task Confirmar_SemUploadPrevio_Recusa()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarAsync();

        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);

        ConferirRecusa(confirmar, "DocumentoAPublicar.ObjetoNaoEncontrado");
    }

    [Fact(DisplayName = "Confirmar objeto sem assinatura PDF válida recusa (422 ConteudoDivergeDoFormato)")]
    public async Task Confirmar_ObjetoSemAssinaturaPdf_Recusa()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarEEnviarAsync([.. "não é um pdf de verdade"u8]);

        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);

        ConferirRecusa(confirmar, "DocumentoAPublicar.ConteudoDivergeDoFormato");
    }

    [Fact(DisplayName = "Confirmar objeto de 0 bytes recusa (422 ArquivoVazio, não 500 — Range GET não é satisfazível sobre objeto vazio)")]
    public async Task Confirmar_ObjetoVazio_Recusa()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await envio.IniciarEEnviarAsync([]);

        Result<DocumentoAPublicarDto> confirmar = await envio.ConfirmarAsync(iniciar.DocumentoAPublicarId);

        ConferirRecusa(confirmar, "DocumentoAPublicar.ArquivoVazio");
    }

    [Fact(DisplayName = "Confirmar um documento já confirmado recusa (imutabilidade — CA)")]
    public async Task Confirmar_DocumentoJaConfirmado_Recusa()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdfValido)).DocumentoAPublicarId;
        (await envio.ConfirmarAsync(documentoId)).IsSuccess.Should().BeTrue();

        Result<DocumentoAPublicarDto> segundaConfirmacao = await envio.ConfirmarAsync(documentoId);

        ConferirRecusa(segundaConfirmacao, "DocumentoAPublicar.StatusInvalidoParaConfirmacao");
    }

    [Fact(DisplayName = "Iniciar upload duas vezes para o mesmo processo cria dois registros com object keys distintas (CA — nunca sobrescreve)")]
    public async Task IniciarUpload_DuasVezes_CriaRegistrosDistintos()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        IniciarEnvioDoDocumentoAPublicarDto primeiro = await envio.IniciarAsync();
        IniciarEnvioDoDocumentoAPublicarDto segundo = await envio.IniciarAsync();

        primeiro.DocumentoAPublicarId.Should().NotBe(segundo.DocumentoAPublicarId);

        DocumentoAPublicar? doc1 = await envio.Documentos.ObterPorIdAsync(primeiro.DocumentoAPublicarId, CancellationToken.None);
        DocumentoAPublicar? doc2 = await envio.Documentos.ObterPorIdAsync(segundo.DocumentoAPublicarId, CancellationToken.None);
        doc1!.ObjectKey.Should().NotBe(doc2!.ObjectKey);
    }

    [Fact(DisplayName = "Falha após reivindicação reverte para pendente e retry converge na mesma cópia selada")]
    public async Task Confirmar_FalhaAoSalvarCopiaSelada_ReverteEPermiteRetrySemOrfaos()
    {
        await using EnvioDoDocumentoAPublicar envio = await NovoEnvioAsync();
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdfValido)).DocumentoAPublicarId;
        await ConfirmarFalhandoAoSalvarACopiaSeladaAsync(envio, documentoId);
        await using EnvioDoDocumentoAPublicar retentativa = envio.EmNovoContexto(_dbFixture);
        string chaveSelada = $"selecao/documentos-a-publicar/{envio.Processo.Id:D}/{documentoId:D}/confirmado.pdf";
        await ConferirPendenteComACopiaSeladaJaGravadaAsync(retentativa, documentoId, chaveSelada);

        Result<DocumentoAPublicarDto> retry = await retentativa.ConfirmarAsync(documentoId);

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.Status.Should().Be("Confirmado");
        retry.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);
        await ConferirCopiaSeladaIntegraAsync(retentativa, documentoId, chaveSelada);
    }

    private static void ConferirRecusa(Result<DocumentoAPublicarDto> confirmar, string codigo)
    {
        confirmar.IsFailure.Should().BeTrue();
        confirmar.Error!.Code.Should().Be(codigo);
    }

    private Task<EnvioDoDocumentoAPublicar> NovoEnvioAsync() => EnvioDoDocumentoAPublicar.NovoAsync(_dbFixture, _storage);

    private async Task<byte[]> LerSeladoAsync(string chave)
    {
        await using Stream selado = await _storage.AbrirLeituraAsync(chave, DocumentoAPublicar.TamanhoMaximoBytes + 1, CancellationToken.None);
        using MemoryStream buffer = new();
        await selado.CopyToAsync(buffer, CancellationToken.None);
        return buffer.ToArray();
    }

    private async Task ConferirCopiaSeladaComOTipoDeMidiaAsync(EnvioDoDocumentoAPublicar envio, Guid documentoId, string tipoDeMidia)
    {
        DocumentoAPublicar gravado = await envio.Context.DocumentosAPublicar.AsNoTracking().SingleAsync(d => d.Id == documentoId);
        InfoObjetoArmazenado? selado = await _storage.ObterInfoAsync(gravado.ObjectKeyConfirmado!);
        selado.Should().NotBeNull();
        selado!.ContentType.Should().Be(tipoDeMidia);
    }

    private async Task ConfirmarFalhandoAoSalvarACopiaSeladaAsync(EnvioDoDocumentoAPublicar envio, Guid documentoId)
    {
        await using IDbContextTransaction transacao = await envio.Context.Database.BeginTransactionAsync();

        Func<Task> confirmar = () => envio.ConfirmarAsync(documentoId, new ArmazenamentoQueFalhaDepoisDeSelar(_storage));

        await confirmar.Should().ThrowAsync<InvalidOperationException>();
        await transacao.RollbackAsync();
    }

    private async Task ConferirPendenteComACopiaSeladaJaGravadaAsync(EnvioDoDocumentoAPublicar envio, Guid documentoId, string chaveSelada)
    {
        DocumentoAPublicar documento = await envio.Context.DocumentosAPublicar.FirstAsync(d => d.Id == documentoId);
        documento.Status.Should().Be(StatusArquivoEnviado.Pendente);
        documento.HashSha256.Should().BeNull();
        documento.ObjectKeyConfirmado.Should().BeNull();
        (await _storage.ObterInfoAsync(chaveSelada, CancellationToken.None)).Should().NotBeNull();
    }

    private async Task ConferirCopiaSeladaIntegraAsync(EnvioDoDocumentoAPublicar envio, Guid documentoId, string chaveSelada)
    {
        DocumentoAPublicar confirmado = await envio.Context.DocumentosAPublicar.AsNoTracking().FirstAsync(d => d.Id == documentoId);
        confirmado.ObjectKeyConfirmado.Should().Be(chaveSelada);
        (await LerSeladoAsync(confirmado.ObjectKeyConfirmado!)).Should().Equal(ConteudoPdfValido);
    }
}
