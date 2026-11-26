namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do FADA
/// regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) e dos critérios de desempate: persiste e
/// recarrega as 5 variantes de
/// <c>ParametrosCriterioDesempate</c> (jsonb polimórfico) e o owned type
/// <c>ReferenciaRegra</c>, e prova a reconfiguração sobre o agregado tracked
/// (a mesma proteção <c>ValueGeneratedNever</c> dos demais filhos do agregado).
/// </summary>
public sealed class DesempateFadaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDoProcessoSeletivo _apoio;

    public DesempateFadaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDoProcessoSeletivo(fixture);
    }

    private static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashSeed[0], 64));

    private static CondicaoDnf CondicaoProfessorRural() =>
        CondicaoDnf.Criar("PROFESSOR_RURAL", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    [Fact(DisplayName = "Persiste e recarrega os 5 critérios de desempate (parâmetros polimórficos) e o FADA")]
    public async Task PersisteERecarrega_DesempateEFada()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convênios 2026");
        EtapaProcesso etapa = EtapaDeRedacao();
        processo.DefinirEtapas([etapa]);
        Guid baseLegalId = Guid.CreateVersion7();
        processo.DefinirCriteriosDesempate(CriteriosDeTodasAsVariantes(etapa.Id)).IsSuccess.Should().BeTrue();
        processo.DefinirFada(aplica: true, configuracao: FadaMultiplicativo(baseLegalId, 1.20m, null, "PEDE_O_ACRESCIMO", "e")).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        ProcessoSeletivo recarregado = await RecarregarComDesempateEFadaAsync(processo.Id);

        recarregado.CriteriosDesempate.Should().HaveCount(5);
        ConferirOsCriteriosRecarregados(recarregado, etapa.Id);
        ConferirOFadaRecarregado(recarregado, baseLegalId);
    }

    [Fact(DisplayName = "Reconfigurar desempate+FADA sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarDesempateFadaSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026 — Reconfig", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        processo.DefinirEtapas([EtapaDeRedacao("RESULTADO_FINAL")]);
        processo.DefinirCriteriosDesempate([CriterioDeMaiorIdade(1, "a")]);
        processo.DefinirFada(aplica: true, configuracao: FadaMultiplicativo(Guid.CreateVersion7(), 1.10m, null, null, "b"));
        await _apoio.GravarAsync(processo);

        await ReconfigurarDesempateEFadaAsync(processo.Id);

        ProcessoSeletivo recarregado = await RecarregarComDesempateEFadaAsync(processo.Id);
        recarregado.CriteriosDesempate.Should().HaveCount(2);
        recarregado.Fada!.Fator.Should().Be(1.30m);
        recarregado.Fada.Teto.Should().Be(5m);
    }

    [Fact(DisplayName = "Atualizar dados da MESMA etapa (Id preservado) mantém o desempate referenciando-a")]
    public async Task AtualizarEtapaMesmoId_MantemDesempateReferenciado()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — PSVR");
        EtapaProcesso etapa = EtapaDeRedacao();
        processo.DefinirEtapas([etapa]);
        processo.DefinirCriteriosDesempate([CriterioDeMaiorNotaEtapa(etapa.Id)]);
        await _apoio.GravarAsync(processo);

        await AtualizarOsDadosDaEtapaTrackedAsync(processo.Id);

        ProcessoSeletivo recarregado = await RecarregarComEtapasEDesempateAsync(processo.Id);

        ConferirAEtapaRevisadaEODesempateQueAReferencia(recarregado, etapa.Id);
    }

    private static ProcessoSeletivo NovoProcesso(string nome, TipoProcessoSnapshot? tipo = null) => ProcessoSeletivo.Criar(
        nome, tipo ?? TiposDeProcessoDeTeste.VagasRemanescentes, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static EtapaProcesso EtapaDeRedacao(string? faseCodigo = null) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoProvaObjetiva(), Ordem: 1, FaseCodigo: faseCodigo),
        DeclaracaoComPeso(1m)).Value!;

    private static TipoEtapaSnapshot TipoProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static DeclaracaoDaAvaliacaoDaEtapa DeclaracaoComPeso(decimal peso) =>
        new(Peso: peso, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null);

    private static CriterioDesempate CriterioDeMaiorNotaEtapa(Guid etapaId) =>
        CriterioDesempate.Criar(1, Regra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo(), "a"), new ParametrosDesempateMaiorNotaEtapa(etapaId)).Value!;

    private static CriterioDesempate CriterioDeMaiorIdade(int ordem, string hashSeed) =>
        CriterioDesempate.Criar(ordem, Regra(RegraDeDesempate.MaiorIdade.Codigo(), hashSeed), new ParametrosDesempateMaiorIdade()).Value!;

    private static CriterioDesempate[] CriteriosDeTodasAsVariantes(Guid etapaId) =>
    [
        CriterioDeMaiorNotaEtapa(etapaId),
        CriterioDesempate.Criar(2, Regra(RegraDeDesempate.Idoso.Codigo(), "b"), new ParametrosDesempateIdoso(60)).Value!,
        CriterioDeMaiorIdade(3, "c"),
        CriterioDesempate.Criar(
            4, Regra(RegraDeDesempate.CondicaoSobreDado.Codigo(), "d"), new ParametrosDesempatePredicadoFato(CondicaoProfessorRural())).Value!,
        CriterioDesempate.Criar(
            5, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), "f"), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA", "LINGUAGENS"])).Value!,
    ];

    private static ConfiguracaoDoFada FadaMultiplicativo(Guid baseLegalId, decimal fator, decimal? teto, string? fatoDeElegibilidade, string hashSeed) =>
        ConfiguracaoDoFada.Criar(
            Regra(RegraDoFada.Multiplicativo.Codigo(), hashSeed), fator, teto, fatoDeElegibilidade,
            new BaseLegalCongelada(baseLegalId, "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!;

    private async Task<ProcessoSeletivo> RecarregarComDesempateEFadaAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Fada!).ThenInclude(b => b.Municipios)
            .FirstOrDefaultAsync(p => p.Id == processoId, CancellationToken.None);

        recarregado.Should().NotBeNull();
        return recarregado!;
    }

    private async Task<ProcessoSeletivo> RecarregarComEtapasEDesempateAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        return await readContext.ProcessosSeletivos
            .Include(p => p.Etapas).Include(p => p.CriteriosDesempate)
            .SingleAsync(p => p.Id == processoId, CancellationToken.None);
    }

    private static void ConferirAEtapaRevisadaEODesempateQueAReferencia(ProcessoSeletivo recarregado, Guid etapaId)
    {
        EtapaProcesso etapaAtualizada = recarregado.Etapas.Single();
        etapaAtualizada.Id.Should().Be(etapaId);
        etapaAtualizada.Nome.Should().Be("Redação (revisada)");
        etapaAtualizada.Peso.Should().Be(2m);
        ((ParametrosDesempateMaiorNotaEtapa)recarregado.CriteriosDesempate.Single().Parametros).EtapaId.Should().Be(etapaId);
    }

    private async Task ReconfigurarDesempateEFadaAsync(Guid processoId)
    {
        await using UniPlusDbContext configureContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        CriterioDesempate idoso = CriterioDesempate.Criar(1, Regra(RegraDeDesempate.Idoso.Codigo(), "c"), new ParametrosDesempateIdoso(65)).Value!;

        carregado.DefinirCriteriosDesempate([idoso, CriterioDeMaiorIdade(2, "d")]).IsSuccess.Should().BeTrue();
        carregado.DefinirFada(aplica: true, configuracao: FadaMultiplicativo(Guid.CreateVersion7(), 1.30m, 5m, null, "e")).IsSuccess.Should().BeTrue();

        await configureContext.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Reproduz a reconciliação do DefinirEtapasCommandHandler: o cliente ecoa o Id lido
    /// anteriormente, o handler ATUALIZA a mesma instância tracked em vez de recriá-la — sem isso,
    /// o etapa_ref do desempate ficaria órfão a cada PUT /etapas.
    /// </summary>
    private async Task AtualizarOsDadosDaEtapaTrackedAsync(Guid processoId)
    {
        await using UniPlusDbContext configureContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        EtapaProcesso etapaTracked = carregado.Etapas.Single();
        etapaTracked.AtualizarDados(
            new IdentificacaoDaEtapa(Nome: "Redação (revisada)", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            DeclaracaoComPeso(2m));

        carregado.DefinirEtapas([etapaTracked]).IsSuccess.Should().BeTrue();

        await configureContext.SaveChangesAsync(CancellationToken.None);
    }

    private static void ConferirOsCriteriosRecarregados(ProcessoSeletivo recarregado, Guid etapaId)
    {
        CriterioDesempate maiorNotaEtapa = recarregado.CriteriosDesempate.Single(c => c.Ordem == 1);
        maiorNotaEtapa.Regra.Codigo.Should().Be(RegraDeDesempate.MaiorNotaNaEtapa.Codigo());
        ((ParametrosDesempateMaiorNotaEtapa)maiorNotaEtapa.Parametros).EtapaId.Should().Be(etapaId);
        ((ParametrosDesempateIdoso)recarregado.CriteriosDesempate.Single(c => c.Ordem == 2).Parametros).IdadeMinima.Should().Be(60);
        recarregado.CriteriosDesempate.Single(c => c.Ordem == 3).Parametros.Should().BeOfType<ParametrosDesempateMaiorIdade>();

        ParametrosDesempatePredicadoFato predicado = (ParametrosDesempatePredicadoFato)recarregado.CriteriosDesempate.Single(c => c.Ordem == 4).Parametros;
        (predicado.Condicao.Fato, predicado.Condicao.Operador, predicado.Condicao.Valor.GetBoolean()).Should().Be(("PROFESSOR_RURAL", Operador.Igual, true));

        // A ordem das áreas é a prioridade do desempate e sobrevive ao round-trip do json.
        ((ParametrosDesempateMaiorNotaAreaEnem)recarregado.CriteriosDesempate.Single(c => c.Ordem == 5).Parametros).Areas
            .Should().Equal("REDACAO", "MATEMATICA", "LINGUAGENS");
    }

    private static void ConferirOFadaRecarregado(ProcessoSeletivo recarregado, Guid baseLegalId)
    {
        recarregado.Fada.Should().NotBeNull();
        ConfiguracaoDoFada fada = recarregado.Fada!;
        fada.Regra.Codigo.Should().Be(RegraDoFada.Multiplicativo.Codigo());
        fada.Fator.Should().Be(1.20m);
        fada.Teto.Should().BeNull();
        fada.FatoElegibilidadeCodigo.Should().Be("PEDE_O_ACRESCIMO");
        fada.BaseLegalId.Should().Be(baseLegalId);
        fada.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208");
    }
}
