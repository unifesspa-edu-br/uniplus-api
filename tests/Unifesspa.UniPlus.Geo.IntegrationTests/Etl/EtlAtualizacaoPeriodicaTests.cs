namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Observability;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Atualização periódica do ETL (Story #674) sobre Postgres+PostGIS real: orquestra os
/// importadores de topo (#672) e folhas (#673), aplica a política de stale na recarga,
/// sela a versão vigente do cache de CEP e persiste status + relatório. Cobre CA-03
/// (versionamento/idempotência), CA-04 (linhas obsoletas), CA-05 (invalidação do cache),
/// CA-07 (observabilidade/registro) e CA-08 (sem PII).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class EtlAtualizacaoPeriodicaTests
{
    private const string Maraba = "1500402";
    private const string SaoPaulo = "3550308";
    private const string CepMaraba = "68500000";
    private const string CepSaoPaulo = "01000000";

    private readonly GeoPostgisFixture _fixture;

    public EtlAtualizacaoPeriodicaTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-03/04/05: recarga atualiza por chave natural, marca linhas ausentes como vigente=false (sem remover) e sela a versão vigente do cache")]
    public async Task Recarga_AtualizaPorChaveNatural_MarcaStale_E_SelaCache()
    {
        await LimparAsync();
        IGeoCepCacheInvalidador cache = Substitute.For<IGeoCepCacheInvalidador>();
        FonteFactoryFake fonteFactory = new(new Dictionary<string, IGeoFonteDados>(StringComparer.Ordinal)
        {
            ["202601"] = FonteCompleta("202601"),
            ["202602"] = FonteSomenteMaraba("202602"),
        });

        Guid id1 = await CriarExecucaoAsync("202601");
        await ExecutarAsync(id1, fonteFactory, cache);

        Guid id2 = await CriarExecucaoAsync("202602");
        await ExecutarAsync(id2, fonteFactory, cache);

        await using GeoDbContext leitura = _fixture.CreateDbContext();

        Cidade maraba = await leitura.Cidades.SingleAsync(c => c.CodigoIbge == Maraba);
        maraba.VersaoDataset.Should().Be("202602", "Marabá foi revisto na nova release");
        maraba.Vigente.Should().BeTrue();

        Cidade saoPaulo = await leitura.Cidades.SingleAsync(c => c.CodigoIbge == SaoPaulo);
        saoPaulo.VersaoDataset.Should().Be("202601", "São Paulo não consta na release 202602");
        saoPaulo.Vigente.Should().BeFalse("linha ausente na nova release é marcada obsoleta");
        (await leitura.Cidades.CountAsync()).Should().Be(2, "stale não remove fisicamente (rastreabilidade)");

        Logradouro avPaulista = await leitura.Logradouros.SingleAsync(l => l.Cep == CepSaoPaulo);
        avPaulista.Vigente.Should().BeFalse("o CEP ausente na nova release fica obsoleto, sem remoção");

        Logradouro ruaA = await leitura.Logradouros.SingleAsync(l => l.Cep == CepMaraba);
        ruaA.VersaoDataset.Should().Be("202602");
        ruaA.Vigente.Should().BeTrue();

        await cache.Received(1).InvalidarAsync("202602", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-07/08: a carga conclui registrando status, relatório por tabela (contadores) e disparador — sem PII")]
    public async Task Carga_RegistraRelatorio_SemPii()
    {
        await LimparAsync();
        IGeoCepCacheInvalidador cache = Substitute.For<IGeoCepCacheInvalidador>();
        FonteFactoryFake fonteFactory = new(new Dictionary<string, IGeoFonteDados>(StringComparer.Ordinal)
        {
            ["202601"] = FonteCompleta("202601"),
        });

        Guid id = await CriarExecucaoAsync("202601");
        await ExecutarAsync(id, fonteFactory, cache);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);

        execucao.Status.Should().Be(StatusImportacao.Concluida);
        execucao.ConcluidoEm.Should().NotBeNull();
        execucao.DisparadoPor.Should().Be("teste");
        execucao.RelatorioJson.Should().NotBeNullOrWhiteSpace();

        RelatorioImportacaoDto? relatorio = JsonSerializer.Deserialize<RelatorioImportacaoDto>(execucao.RelatorioJson!);
        relatorio.Should().NotBeNull();
        relatorio!.VersaoDataset.Should().Be("202601");
        relatorio.Tabelas.Should().Contain(t => t.Tabela == "cidade" && t.Inseridos >= 2);
        relatorio.Inseridos.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "Robustez: falha na carga marca a execução como Falhou e NÃO sela o cache (a versão vigente não mudou)")]
    public async Task Falha_MarcaFalhou_E_NaoSelaCache()
    {
        await LimparAsync();
        IGeoCepCacheInvalidador cache = Substitute.For<IGeoCepCacheInvalidador>();
        FonteFactoryFake fonteFactory = new(new Dictionary<string, IGeoFonteDados>(StringComparer.Ordinal)
        {
            ["202601"] = new FonteComFalhaNasCidades(FonteCompleta("202601")),
        });

        Guid id = await CriarExecucaoAsync("202601");
        await ExecutarAsync(id, fonteFactory, cache);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);

        execucao.Status.Should().Be(StatusImportacao.Falhou);
        execucao.ConcluidoEm.Should().NotBeNull();
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Robustez: cancelamento/desligamento durante a carga marca a execução Falhou (libera o índice) e não sela o cache")]
    public async Task Cancelamento_MarcaFalhou_E_NaoSelaCache()
    {
        await LimparAsync();
        IGeoCepCacheInvalidador cache = Substitute.For<IGeoCepCacheInvalidador>();
        FonteFactoryCancelada fonteFactory = new();

        Guid id = await CriarExecucaoAsync("202601");
        Func<Task> acao = () => ExecutarAsync(id, fonteFactory, cache);

        await acao.Should().ThrowAsync<OperationCanceledException>();

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);
        execucao.Status.Should().Be(StatusImportacao.Falhou, "a instância dona marca terminal já, não deixa EmAndamento bloqueando");
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Robustez: cache indisponível ao selar (ex.: Redis fora resolvido pelo Lazy) não reverte a carga já concluída")]
    public async Task SeloComCacheIndisponivel_NaoFalhaCarga()
    {
        await LimparAsync();
        FonteFactoryFake fonteFactory = new(new Dictionary<string, IGeoFonteDados>(StringComparer.Ordinal)
        {
            ["202601"] = FonteCompleta("202601"),
        });
        // Lazy que lança ao resolver — simula o IConnectionMultiplexer.Connect falhando no .Value.
        Lazy<IGeoCepCacheInvalidador> cacheQuebrado = new(() => throw new InvalidOperationException("Redis fora do ar"));

        Guid id = await CriarExecucaoAsync("202601");
        Func<Task> acao = () => ExecutarComLazyAsync(id, fonteFactory, cacheQuebrado);

        await acao.Should().NotThrowAsync();

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);
        execucao.Status.Should().Be(StatusImportacao.Concluida, "falha de cache ao selar é best-effort, não pode reverter a carga");
    }

    private async Task<Guid> CriarExecucaoAsync(string versao)
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = GeoImportacaoExecucao
            .Iniciar(versao, "teste", TimeProvider.System.GetUtcNow())
            .Value!;
        ctx.ImportacaoExecucoes.Add(execucao);
        await ctx.SaveChangesAsync();
        return execucao.Id;
    }

    private Task ExecutarAsync(Guid execucaoId, IGeoFonteDadosFactory fonteFactory, IGeoCepCacheInvalidador cache) =>
        ExecutarComLazyAsync(execucaoId, fonteFactory, new Lazy<IGeoCepCacheInvalidador>(() => cache));

    private async Task ExecutarComLazyAsync(Guid execucaoId, IGeoFonteDadosFactory fonteFactory, Lazy<IGeoCepCacheInvalidador> cacheLazy)
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        using GeoEtlMetrics metricas = new();
        GeoEtlOrquestrador orquestrador = CriarOrquestrador(ctx, fonteFactory, cacheLazy, metricas);
        await orquestrador.ExecutarAsync(execucaoId, CancellationToken.None);
    }

    private GeoEtlOrquestrador CriarOrquestrador(
        GeoDbContext ctx,
        IGeoFonteDadosFactory fonteFactory,
        Lazy<IGeoCepCacheInvalidador> cacheLazy,
        GeoEtlMetrics metricas)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GeoDb"] = _fixture.ConnectionString })
            .Build();

        GeoImportadorPaisEstadoCidade topo = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
        GeoImportadorDistritoBairro distritoBairro = new(ctx);
        LogradouroCopyImporter logradouro = new(configuracao, TimeProvider.System, NullLogger<LogradouroCopyImporter>.Instance);
        GeoImportadorLocalidades folhas = new(distritoBairro, logradouro, NullLogger<GeoImportadorLocalidades>.Instance);

        return new GeoEtlOrquestrador(
            ctx,
            topo,
            folhas,
            fonteFactory,
            cacheLazy,
            new GeoImportacaoFila(),
            metricas,
            TimeProvider.System,
            NullLogger<GeoEtlOrquestrador>.Instance);
    }

    private async Task LimparAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE pais, logradouro_complemento, cep_grande_usuario, geo_importacao_execucao CASCADE");
    }

    private static FonteEmMemoria FonteCompleta(string versao)
    {
        FonteEmMemoria fonte = new() { Versao = versao };

        fonte.Paises.Add(DadosDne.Pais("BRA", "Brasil", "BR"));
        fonte.Estados.Add(DadosDne.Estado("PA", "Pará", capital: "Belém", faixaIni: "66000000", faixaFim: "68899999"));
        fonte.Estados.Add(DadosDne.Estado("SP", "São Paulo", regiao: "Sudeste", capital: "São Paulo", faixaIni: "01000000", faixaFim: "19999999"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("PA", "15"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("SP", "35"));
        fonte.Cidades.Add(DadosDne.Cidade(Maraba, "Marabá", "PA", ddd: "94"));
        fonte.Cidades.Add(DadosDne.Cidade(SaoPaulo, "São Paulo", "SP", ddd: "11"));
        fonte.CidadeIds.Add(DadosDne.CidadeId(1, Maraba));
        fonte.CidadeIds.Add(DadosDne.CidadeId(2, SaoPaulo));

        fonte.Distritos.Add(DadosDne.Distrito(10, "Cidade Nova", 1));
        fonte.Distritos.Add(DadosDne.Distrito(20, "Centro", 2));
        fonte.Logradouros.Add(DadosDne.Logradouro(CepMaraba, "Rua A", 1, distritoIdDne: 10));
        fonte.Logradouros.Add(DadosDne.Logradouro(CepSaoPaulo, "Av Paulista", 2, distritoIdDne: 20));

        return fonte;
    }

    // Release seguinte sem São Paulo (cidade + CEP somem) — exercita a política de stale.
    private static FonteEmMemoria FonteSomenteMaraba(string versao)
    {
        FonteEmMemoria fonte = new() { Versao = versao };

        fonte.Paises.Add(DadosDne.Pais("BRA", "Brasil", "BR"));
        fonte.Estados.Add(DadosDne.Estado("PA", "Pará", capital: "Belém", faixaIni: "66000000", faixaFim: "68899999"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("PA", "15"));
        fonte.Cidades.Add(DadosDne.Cidade(Maraba, "Marabá", "PA", ddd: "94"));
        fonte.CidadeIds.Add(DadosDne.CidadeId(1, Maraba));

        fonte.Distritos.Add(DadosDne.Distrito(10, "Cidade Nova", 1));
        fonte.Logradouros.Add(DadosDne.Logradouro(CepMaraba, "Rua A", 1, distritoIdDne: 10));

        return fonte;
    }

    private sealed class FonteFactoryFake : IGeoFonteDadosFactory
    {
        private readonly IReadOnlyDictionary<string, IGeoFonteDados> _porVersao;

        public FonteFactoryFake(IReadOnlyDictionary<string, IGeoFonteDados> porVersao)
        {
            _porVersao = porVersao;
        }

        public IGeoFonteDados Criar(string versao) => _porVersao[versao];
    }

    // Simula cancelamento (shutdown) no meio da carga: o orquestrador deve marcar a execução
    // terminal e repropagar o OperationCanceledException.
    private sealed class FonteFactoryCancelada : IGeoFonteDadosFactory
    {
        public IGeoFonteDados Criar(string versao) => throw new OperationCanceledException();
    }
}
