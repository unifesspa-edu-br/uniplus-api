namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Postgres e MinIO em containers) dos arquivos do candidato: a leitura sai do bucket definitivo
/// com nome genérico, a limpeza só toca o bucket definitivo, o pendente vencido sai e o em verificação nunca, e a
/// reconciliação tem uma instância só por vez.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DocumentoDaInscricaoIntegrationTests : IClassFixture<InscricaoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string BucketDeEnvio = "uniplus-candidato-envio-test";
    private const string BucketDefinitivo = "uniplus-candidato-definitivo-test";

    private static readonly byte[] Pdf = [.. "%PDF-1.7 laudo de teste"u8];

    private readonly InscricaoDbFixture _db;
    private readonly IStorageService _bucketes;
    private readonly ArquivoDoCandidatoStorageService _storage;

    public DocumentoDaInscricaoIntegrationTests(InscricaoDbFixture db, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(minio);
        _db = db;

        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = "uniplus-documentos-test",
        };
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        ServiceProvider provider = services.BuildServiceProvider();
        _bucketes = provider.GetRequiredService<IStorageService>();
        _storage = new ArquivoDoCandidatoStorageService(
            new ArmazenamentoDoCandidato(_bucketes), Options.Create(new ArquivosDoCandidatoOptions { BucketDeEnvio = BucketDeEnvio, BucketDefinitivo = BucketDefinitivo }));
    }

    private async Task<(SelecaoDbContext Context, DocumentoDaInscricaoRepository Repositorio, ArquivoDaApresentacao Arquivo)> PendenteAsync()
    {
        SelecaoDbContext context = _db.CreateDbContext();
        InscricaoRepository inscricoes = new(context, _db.Encryption, _db.BlindIndex, TimeProvider.System);
        Inscricao inscricao = Inscricao.Iniciar(Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        await inscricoes.AdicionarAsync(inscricao);

        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, laudo, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, laudo, "application/pdf", Pdf.Length, TimeProvider.System, PrazosDoArquivoEnviado.Envio).Value!;
        DocumentoDaInscricaoRepository repositorio = new(context);
        await repositorio.AdicionarApresentacaoAsync(apresentacao);
        await repositorio.AdicionarArquivoAsync(arquivo);
        await context.SaveChangesAsync();
        return (context, repositorio, arquivo);
    }

    private async Task GravarAsync(string bucket, string chave)
    {
        using MemoryStream conteudo = new(Pdf);
        await _bucketes.UploadAsync(bucket, chave, conteudo, "application/pdf");
    }

    [Fact(DisplayName = "A URL de leitura entrega o arquivo do bucket definitivo como download, com nome genérico codificado em filename*, sem a chave do objeto")]
    public async Task Leitura_UrlEntregaAttachmentComNomeGenerico()
    {
        (SelecaoDbContext context, _, ArquivoDaApresentacao arquivo) = await PendenteAsync();
        await GravarAsync(BucketDefinitivo, arquivo.ChaveDefinitivaPrevista);

        string url = await _storage.GerarUrlLeituraAsync(arquivo.ChaveDefinitivaPrevista, "Arquivo 1.pdf", TimeSpan.FromMinutes(1));

        using HttpClient http = new();
        using HttpResponseMessage resposta = await http.GetAsync(new Uri(url));
        resposta.EnsureSuccessStatusCode();
        resposta.Content.Headers.ContentDisposition!.ToString().Should().Be("attachment; filename*=UTF-8''Arquivo%201.pdf");
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(Pdf);
        resposta.Content.Headers.ContentDisposition.ToString().Should().NotContain(arquivo.Id.ToString("D"));
        context.Dispose();
    }

    [Fact(DisplayName = "A disposição vai na assinatura: trocar attachment por inline na URL de leitura é recusado pelo armazenamento")]
    public async Task Leitura_DisposicaoAdulteradaERecusada()
    {
        (SelecaoDbContext context, _, ArquivoDaApresentacao arquivo) = await PendenteAsync();
        await GravarAsync(BucketDefinitivo, arquivo.ChaveDefinitivaPrevista);

        string url = await _storage.GerarUrlLeituraAsync(arquivo.ChaveDefinitivaPrevista, "Arquivo 1.pdf", TimeSpan.FromMinutes(1));
        string adulterada = url.Replace("attachment", "inline", StringComparison.Ordinal);

        adulterada.Should().NotBe(url, "a disposição está na query da URL assinada");
        using HttpClient http = new();
        using HttpResponseMessage resposta = await http.GetAsync(new Uri(adulterada));
        resposta.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        context.Dispose();
    }

    [Fact(DisplayName = "O banco recusa a apresentação sem nome do documento")]
    public async Task Apresentacao_NomeDoDocumentoVazio_BancoRecusa()
    {
        await using SelecaoDbContext context = _db.CreateDbContext();

        Func<Task> inserir = async () => await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO selecao.apresentacoes_de_documento (id, inscricao_id, exigencia_id, nome_do_documento, created_at) " +
            "VALUES ({0}, {1}, {2}, ' ', now())",
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        Npgsql.PostgresException erro = (await inserir.Should().ThrowAsync<Exception>()).Which as Npgsql.PostgresException
            ?? throw new InvalidOperationException("O banco devia recusar com uma violação de CHECK.");
        erro.SqlState.Should().Be("23514");
        erro.ConstraintName.Should().Be("ck_apresentacoes_de_documento_nome_do_documento_preenchido");
    }

    [Fact(DisplayName = "O pendente vencido sai com a apresentação que ficou sem arquivo, e o arquivo em verificação nunca sai")]
    public async Task RemoverPendenteVencido_LimpaAApresentacaoSemArquivo_EPoupaOEmVerificacao()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao pendente) = await PendenteAsync();
        (SelecaoDbContext outroContext, DocumentoDaInscricaoRepository outroRepositorio, ArquivoDaApresentacao emVerificacao) = await PendenteAsync();
        emVerificacao.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        outroRepositorio.Atualizar(emVerificacao);
        await outroContext.SaveChangesAsync();
        DateTimeOffset depoisDoPrazo = DateTimeOffset.UtcNow.AddDays(1);

        (await repositorio.RemoverSePendenteVencidoAsync(pendente.Id, depoisDoPrazo)).Should().BeTrue();
        (await outroRepositorio.RemoverSePendenteVencidoAsync(emVerificacao.Id, depoisDoPrazo)).Should().BeFalse();

        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ArquivosDaApresentacao.AnyAsync(a => a.Id == pendente.Id)).Should().BeFalse();
        (await leitura.ApresentacoesDeDocumento.AnyAsync(a => a.Id == pendente.ApresentacaoId)).Should().BeFalse();
        (await leitura.ArquivosDaApresentacao.AnyAsync(a => a.Id == emVerificacao.Id)).Should().BeTrue();
        context.Dispose();
        outroContext.Dispose();
    }

    [Fact(DisplayName = "A conclusão da verificação é atômica: só a primeira transição de um arquivo em verificação grava, a segunda não altera a linha")]
    public async Task TentarConcluirVerificacao_SoAPrimeiraTransicaoGrava()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteAsync();
        arquivo.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        repositorio.Atualizar(arquivo);
        await context.SaveChangesAsync();

        (await repositorio.TentarConcluirVerificacaoAsync(arquivo.Id, SituacaoDoArquivoDoCandidato.Recusado)).Should().BeTrue();
        (await repositorio.TentarConcluirVerificacaoAsync(arquivo.Id, SituacaoDoArquivoDoCandidato.ReenvioNecessario)).Should().BeFalse();

        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ArquivosDaApresentacao.AsNoTracking().SingleAsync(a => a.Id == arquivo.Id)).Situacao
            .Should().Be(SituacaoDoArquivoDoCandidato.Recusado);
        context.Dispose();
    }

    [Fact(DisplayName = "A rotina apaga só a chave definitiva do arquivo removido e deixa o bucket de envio para a expiração dele")]
    public async Task Rotina_ApagaSoADefinitiva()
    {
        (SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, ArquivoDaApresentacao arquivo) = await PendenteAsync();
        arquivo.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        arquivo.RegistrarVerificado(Pdf.Length, new string('a', 64), FormatoPermitido.Pdf).Should().BeTrue();
        await GravarAsync(BucketDeEnvio, arquivo.ObjectKey);
        await GravarAsync(BucketDefinitivo, arquivo.ChaveDefinitivaPrevista);
        await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(repositorio, arquivo, DateTimeOffset.UtcNow, CancellationToken.None);
        await context.SaveChangesAsync();

        await Rotina(context, repositorio, DateTimeOffset.UtcNow).ExecutarAsync(CancellationToken.None);

        (await _bucketes.ObterMetadadosAsync(BucketDefinitivo, arquivo.ChaveDefinitivaPrevista)).Should().BeNull();
        (await _bucketes.ObterMetadadosAsync(BucketDeEnvio, arquivo.ObjectKey)).Should().NotBeNull("a API não remove nada do bucket de envio");
        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ChavesALimpar.AnyAsync(c => c.ObjectKey == arquivo.ChaveDefinitivaPrevista)).Should().BeFalse("a chave sai da agenda");
        context.Dispose();
    }

    [Fact(DisplayName = "Duas réplicas na reconciliação: só a que obtém a exclusividade da transação executa, e a outra executa depois que a primeira termina")]
    public async Task Reconciliacao_ExclusividadePorTransacao()
    {
        await using SelecaoDbContext primeira = _db.CreateDbContext();
        await using SelecaoDbContext segunda = _db.CreateDbContext();
        DocumentoDaInscricaoRepository repoPrimeira = new(primeira), repoSegunda = new(segunda);

        await using (IDbContextTransaction transacao = await primeira.Database.BeginTransactionAsync())
        {
            (await repoPrimeira.TentarObterExclusividadeDaReconciliacaoAsync()).Should().BeTrue();

            await using IDbContextTransaction concorrente = await segunda.Database.BeginTransactionAsync();
            (await repoSegunda.TentarObterExclusividadeDaReconciliacaoAsync()).Should().BeFalse();
        }

        await using IDbContextTransaction depois = await segunda.Database.BeginTransactionAsync();
        (await repoSegunda.TentarObterExclusividadeDaReconciliacaoAsync()).Should().BeTrue("a exclusividade acaba com a transação");
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Port da Application, o mesmo que a rotina real consome.")]
    private RemocaoDeArquivosPendentesVencidos Rotina(SelecaoDbContext context, DocumentoDaInscricaoRepository repositorio, DateTimeOffset agora) => new(
        new DocumentoAPublicarRepository(context), new ModeloDeDocumentoRepository(context), repositorio,
        Substitute.For<IArquivoArmazenadoStorage>(), _storage, new RelogioFixo(agora), NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);
}
