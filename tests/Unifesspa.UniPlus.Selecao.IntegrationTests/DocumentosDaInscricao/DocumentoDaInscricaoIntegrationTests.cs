namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Antivirus;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Postgres, MinIO e clamd em containers) da confirmação do arquivo que o candidato
/// envia: o antivírus olha o conteúdo antes de qualquer gravação e o objeto selado só abre decifrado.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DocumentoDaInscricaoIntegrationTests
    : IClassFixture<InscricaoDbFixture>, IClassFixture<MinioContainerFixture>, IClassFixture<ClamAvContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-da-inscricao-test";

    // O arquivo de teste do EICAR dentro de um stream de PDF: começa por %PDF-, como o formato exige, e o
    // clamd o aponta.
    private static readonly byte[] PdfComEicar = [.. "%PDF-1.7\n1 0 obj<</Length 68>>stream\nX5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*\nendstream endobj\n%%EOF"u8];
    private static readonly byte[] PdfLimpo = [.. "%PDF-1.7 laudo de teste — nada a declarar"u8];

    private readonly InscricaoDbFixture _db;
    private readonly ClamAvContainerFixture _clamav;
    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Intencional: o teste exercita o port da Application (IArquivoArmazenadoStorage), o mesmo contrato que os handlers reais consomem.")]
    private readonly IArquivoArmazenadoStorage _storage;

    public DocumentoDaInscricaoIntegrationTests(InscricaoDbFixture db, MinioContainerFixture minio, ClamAvContainerFixture clamav)
    {
        ArgumentNullException.ThrowIfNull(minio);
        _db = db;
        _clamav = clamav;

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

    private ProtecaoDoConteudoSelado Protecao(int? porta = null) => new(
        new ClamdClient(
            Options.Create(new ClamdOptions { Host = _clamav.Host, Port = porta ?? _clamav.Port, TimeoutSegundos = 20 }),
            NullLogger<ClamdClient>.Instance),
        _db.Encryption);

    /// <summary>A inscrição persistida e um arquivo pendente dela, com o objeto enviado ao armazenamento.</summary>
    private async Task<(SelecaoDbContext Context, DocumentoDaInscricaoRepository Repositorio, ArquivoDaApresentacao Arquivo)> PendenteComObjetoAsync(byte[] conteudo)
    {
        SelecaoDbContext context = _db.CreateDbContext();
        InscricaoRepository inscricoes = new(context, _db.Encryption, _db.BlindIndex, TimeProvider.System);
        Inscricao inscricao = Inscricao.Iniciar(Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        await inscricoes.AdicionarAsync(inscricao);

        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, laudo.Id, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, laudo, "application/pdf", conteudo.Length, TimeProvider.System, PrazosDoArquivoEnviado.Envio).Value!;
        DocumentoDaInscricaoRepository repositorio = new(context);
        await repositorio.AdicionarApresentacaoAsync(apresentacao);
        await repositorio.AdicionarArquivoAsync(arquivo);
        await context.SaveChangesAsync();

        string url = await _storage.GerarUrlUploadAsync(arquivo.ObjectKey, arquivo.TipoDeMidia, PrazosDoArquivoEnviado.Envio);
        using HttpClient http = new();
        using ByteArrayContent corpo = new(conteudo);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(arquivo.TipoDeMidia);
        (await http.PutAsync(new Uri(url), corpo)).EnsureSuccessStatusCode();
        return (context, repositorio, arquivo);
    }

    private Task<Result> ConfirmarAsync(DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo, ProtecaoDoConteudoSelado protecao) =>
        ConfirmacaoDoArquivoEnviado.ConfirmarAsync(
            arquivo, arquivo.TamanhoMaximoBytes,
            new ConfirmacaoDoArquivoEnviado.Recusas(
                new DomainError(DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado, "objeto"),
                ArquivoDaApresentacao.TamanhoExcedidoAte(arquivo.TamanhoMaximoBytes),
                ArquivoDaApresentacao.StatusInvalidoParaConfirmacao),
            repositorio, _storage, TimeProvider.System, CancellationToken.None, protecao);

    private async Task<byte[]> LerObjetoAsync(string chave)
    {
        Stream stream = await _storage.AbrirLeituraAsync(chave, 20 * 1024 * 1024);
        await using (stream)
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }

    [Fact(DisplayName = "Arquivo limpo: o objeto selado não abre sem decifrar, a chave leva só o id e o hash é o do conteúdo em claro")]
    public async Task Confirmar_Limpo_SelaCifrado()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteComObjetoAsync(PdfLimpo);
        ProtecaoDoConteudoSelado protecao = Protecao();

        Result resultado = await ConfirmarAsync(repositorio, arquivo, protecao);
        repositorio.Atualizar(arquivo);
        await context.SaveChangesAsync();

        resultado.IsSuccess.Should().BeTrue();
        await using SelecaoDbContext leitura = _db.CreateDbContext();
        ArquivoDaApresentacao gravado = await leitura.ArquivosDaApresentacao.AsNoTracking().SingleAsync(a => a.Id == arquivo.Id);
        gravado.Status.Should().Be(StatusArquivoEnviado.Confirmado);
        gravado.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(PdfLimpo)));
        gravado.ObjectKeyConfirmado.Should().Be($"selecao/documentos-da-inscricao/{arquivo.Id:D}/selado");

        byte[] selado = await LerObjetoAsync(gravado.ObjectKeyConfirmado!);
        selado.AsSpan().IndexOf(PdfLimpo.AsSpan(0, 20)).Should().Be(-1, "o conteúdo selado está cifrado");
        selado.AsSpan().StartsWith("%PDF-"u8).Should().BeFalse();

        Result<byte[]> aberto = await protecao.AbrirAsync(selado, gravado.HashSha256!);
        aberto.Value.Should().Equal(PdfLimpo);

        // O objeto trocado por outro registro tem outro hash e não abre.
        Result<byte[]> trocado = await protecao.AbrirAsync(selado, Convert.ToHexStringLower(SHA256.HashData(PdfComEicar)));
        trocado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ConteudoAdulterado);
    }

    [Fact(DisplayName = "Arquivo com o EICAR: a confirmação é recusada, o arquivo continua pendente e nada é selado")]
    public async Task Confirmar_Infectado_NaoGravaNada()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteComObjetoAsync(PdfComEicar);

        Result resultado = await ConfirmarAsync(repositorio, arquivo, Protecao());

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoInfectado);
        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ArquivosDaApresentacao.AsNoTracking().SingleAsync(a => a.Id == arquivo.Id)).Status
            .Should().Be(StatusArquivoEnviado.Pendente);
        (await _storage.ObterInfoAsync(arquivo.ChaveSelada)).Should().BeNull();
        context.Dispose();
    }

    [Fact(DisplayName = "Com o clamd fora do ar a confirmação é recusada como indisponível e o arquivo continua pendente")]
    public async Task Confirmar_ClamdForaDoAr_ContinuaPendente()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteComObjetoAsync(PdfLimpo);
        using TcpListener livre = new(IPAddress.Loopback, 0);
        livre.Start();
        int portaSemEscuta = ((IPEndPoint)livre.LocalEndpoint).Port;
        livre.Stop();

        Result resultado = await ConfirmarAsync(repositorio, arquivo, Protecao(portaSemEscuta));

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.AntivirusIndisponivel);
        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ArquivosDaApresentacao.AsNoTracking().SingleAsync(a => a.Id == arquivo.Id)).Status
            .Should().Be(StatusArquivoEnviado.Pendente);
        (await _storage.ObterInfoAsync(arquivo.ChaveSelada)).Should().BeNull();
        context.Dispose();
    }

    [Fact(DisplayName = "O pendente vencido sai com a apresentação que ficou sem arquivo, e o confirmado nunca sai")]
    public async Task RemoverPendenteVencido_LimpaAApresentacaoSemArquivo()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao pendente) = await PendenteComObjetoAsync(PdfLimpo);
        (SelecaoDbContext outroContext, DocumentoDaInscricaoRepository outroRepositorio, ArquivoDaApresentacao confirmado) = await PendenteComObjetoAsync(PdfLimpo);
        (await ConfirmarAsync(outroRepositorio, confirmado, Protecao())).IsSuccess.Should().BeTrue();
        outroRepositorio.Atualizar(confirmado);
        await outroContext.SaveChangesAsync();
        DateTimeOffset depoisDoPrazo = DateTimeOffset.UtcNow.AddDays(1);

        (await repositorio.RemoverSePendenteVencidoAsync(pendente.Id, depoisDoPrazo)).Should().BeTrue();
        (await outroRepositorio.RemoverSePendenteVencidoAsync(confirmado.Id, depoisDoPrazo)).Should().BeFalse();

        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ArquivosDaApresentacao.AnyAsync(a => a.Id == pendente.Id)).Should().BeFalse();
        (await leitura.ApresentacoesDeDocumento.AnyAsync(a => a.Id == pendente.ApresentacaoId)).Should().BeFalse();
        (await leitura.ArquivosDaApresentacao.AnyAsync(a => a.Id == confirmado.Id)).Should().BeTrue();
        context.Dispose();
        outroContext.Dispose();
    }

    private RemocaoDeArquivosPendentesVencidos Rotina(SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, DateTimeOffset agora) => new(
        new DocumentoAPublicarRepository(context), new ModeloDeDocumentoRepository(context), repositorio, _storage,
        new RelogioFixo(agora), NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);

    [Fact(DisplayName = "Chave agendada: o objeto recriado em claro pela URL ainda válida é apagado pela rotina depois do prazo, não antes")]
    public async Task Rotina_ApagaAChaveAgendadaSoDepoisDoPrazo()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteComObjetoAsync(PdfLimpo);
        await _storage.RemoverAsync(arquivo.ObjectKey);
        await repositorio.AgendarLimpezaAsync(arquivo.ObjectKey, arquivo.ExpiraEm + RemocaoDeArquivosPendentesVencidos.Tolerancia);
        await context.SaveChangesAsync();

        // A URL pré-assinada ainda vale: um novo PUT recria o conteúdo em claro.
        await _storage.SalvarConteudoSeladoAsync(arquivo.ObjectKey, PdfLimpo, "application/pdf");

        await Rotina(context, repositorio, DateTimeOffset.UtcNow).ExecutarAsync(CancellationToken.None);
        (await _storage.ObterInfoAsync(arquivo.ObjectKey)).Should().NotBeNull("a URL ainda não venceu");

        await Rotina(context, repositorio, DateTimeOffset.UtcNow.AddHours(3)).ExecutarAsync(CancellationToken.None);

        (await _storage.ObterInfoAsync(arquivo.ObjectKey)).Should().BeNull();
        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ChavesALimpar.AnyAsync(c => c.ObjectKey == arquivo.ObjectKey)).Should().BeFalse("a chave sai da agenda");
        context.Dispose();
    }

    [Fact(DisplayName = "Pendente vencido com a cópia selada já gravada (commit que falhou): as duas chaves somem")]
    public async Task Rotina_PendenteVencidoComSelada_ApagaAsDuasChaves()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteComObjetoAsync(PdfLimpo);
        await _storage.SalvarConteudoSeladoAsync(arquivo.ChaveSelada, [1, 2, 3], "application/octet-stream");

        await Rotina(context, repositorio, DateTimeOffset.UtcNow.AddHours(3)).ExecutarAsync(CancellationToken.None);

        (await _storage.ObterInfoAsync(arquivo.ObjectKey)).Should().BeNull();
        (await _storage.ObterInfoAsync(arquivo.ChaveSelada)).Should().BeNull();
        context.Dispose();
    }
}
