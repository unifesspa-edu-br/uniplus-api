namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que o tipo de etapa admitia e a origem da nota no ENEM ficam congelados nas colunas do
/// snapshot da etapa, contra Postgres real: os valores sobrevivem à gravação, e a regravação
/// dos sinalizadores sobre a etapa tracked — uma instância nova do snapshot, mantida a
/// identidade — é persistida por change detection.
/// </summary>
public sealed class SinalizadoresDoTipoDeEtapaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDoProcessoSeletivo _apoio;

    public SinalizadoresDoTipoDeEtapaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName = "Os sinalizadores congelados e a regravação deles sobre a etapa tracked sobrevivem ao banco")]
    public async Task Sinalizadores_PersistemERegravacaoTambem()
    {
        EtapaProcesso etapa = NovaEtapa(
            "Análise documental", CaraterEtapa.Eliminatoria, TipoDeEtapa("ANALISE_DOCUMENTAL", "Análise documental", pontua: false, elimina: true), 1, Declaracao(peso: null, notaMinima: 5m));
        EtapaProcesso prova = EtapaDeProvaObjetiva(2);
        ProcessoSeletivo processo = ProcessoComEtapas("PS 2026 — PSVR", etapa, prova);
        await _apoio.GravarAsync(processo);

        await RegravarOsSinalizadoresDaAnaliseDocumentalAsync(processo.Id, etapa.Id);

        ConferirOsSinalizadoresRegravadosDaAnalise(await RecarregarAsync(processo.Id), etapa.Id, prova.Id);
    }

    [Fact(DisplayName = "A nota de origem no ENEM congelada sobrevive ao banco e à regravação dos sinalizadores")]
    public async Task NotaDeOrigemNoEnem_PersisteERegravacaoAPreserva()
    {
        EtapaProcesso doEnem = NovaEtapa(
            "Nota do ENEM", CaraterEtapa.Classificatoria, TipoDeEtapa("NOTA_ENEM", "Nota do ENEM", pontua: true, elimina: true, notaDoEnem: true), 1, Declaracao(peso: 1m, notaMinima: null));
        EtapaProcesso prova = EtapaDeProvaObjetiva(2);
        ProcessoSeletivo processo = ProcessoComEtapas("PS 2027 — Medicina", doEnem, prova);
        await _apoio.GravarAsync(processo);

        await RegravarOsSinalizadoresDaNotaDoEnemAsync(processo.Id, doEnem.Id, prova.Id);

        TipoEtapaSnapshot regravado = TipoDaEtapa(await RecarregarAsync(processo.Id), doEnem.Id);
        regravado.AdmiteEliminacao.Should().BeFalse("a regravação dos sinalizadores chegou ao banco");
        regravado.NotaDeOrigemNoEnem.Should().BeTrue("a regravação dos sinalizadores preserva a identidade");
    }

    [Fact(DisplayName = "A correção independente congelada sobrevive ao banco e a regravação dos sinalizadores a relê")]
    public async Task CorrecaoIndependente_PersisteERegravacaoARele()
    {
        EtapaProcesso redacao = NovaEtapa(
            "Redação", CaraterEtapa.Classificatoria, TipoDeEtapa("REDACAO", "Redação", pontua: true, elimina: true, exigeCorrecaoIndependente: true), 1, Declaracao(peso: 1m, notaMinima: null));
        ProcessoSeletivo processo = ProcessoComEtapas("PS 2028 — Redação", redacao);
        await _apoio.GravarAsync(processo);

        await ReconfigurarAsync(processo.Id, carregado =>
        {
            EtapaProcesso tracked = carregado.Etapas.Single();
            tracked.TipoEtapa.ExigeCorrecaoIndependente.Should().BeTrue("o sinalizador congelado chegou ao banco");
            Regravar(tracked, tracked.TipoEtapa.ComSinalizadores(true, true, false), Declaracao(peso: 1m, notaMinima: null));
        });

        TipoDaEtapa(await RecarregarAsync(processo.Id), redacao.Id).ExigeCorrecaoIndependente.Should().BeFalse("a regravação dos sinalizadores chegou ao banco");
    }

    private static ProcessoSeletivo ProcessoComEtapas(string nome, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static TipoEtapaSnapshot TipoDeEtapa(
        string codigo, string nome, bool pontua, bool elimina, bool notaDoEnem = false, bool exigeCorrecaoIndependente = false) =>
        TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), codigo, nome, admitePontuacao: pontua, admiteEliminacao: elimina,
            notaDeOrigemNoEnem: notaDoEnem, exigeCorrecaoIndependente: exigeCorrecaoIndependente).Value!;

    private static DeclaracaoDaAvaliacaoDaEtapa Declaracao(decimal? peso, decimal? notaMinima) =>
        new(Peso: peso, NotaMinima: notaMinima, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null);

    private static EtapaProcesso NovaEtapa(string nome, CaraterEtapa carater, TipoEtapaSnapshot tipo, int ordem, DeclaracaoDaAvaliacaoDaEtapa declaracao) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar($"etapa_{ordem}").Value!, new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: tipo, Ordem: ordem, FaseCodigo: null), declaracao).Value!;

    private static EtapaProcesso EtapaDeProvaObjetiva(int ordem) =>
        NovaEtapa("Prova", CaraterEtapa.Classificatoria, TipoDeEtapa("PROVA_OBJETIVA", "Prova Objetiva", pontua: true, elimina: true), ordem, Declaracao(peso: 1m, notaMinima: null));

    private static void Regravar(EtapaProcesso tracked, TipoEtapaSnapshot tipo, DeclaracaoDaAvaliacaoDaEtapa declaracao) =>
        tracked.AtualizarDados(
            new IdentificacaoDaEtapa(Nome: tracked.Nome, Carater: tracked.Carater, TipoEtapa: tipo, Ordem: tracked.Ordem, FaseCodigo: null),
            declaracao).IsSuccess.Should().BeTrue();

    private static void ConferirOsSinalizadoresRegravadosDaAnalise(ProcessoSeletivo recarregado, Guid analiseId, Guid provaId)
    {
        TipoEtapaSnapshot regravado = TipoDaEtapa(recarregado, analiseId);
        (regravado.AdmitePontuacao, regravado.AdmiteEliminacao).Should().Be((true, true));
        (regravado.Codigo, regravado.Nome).Should().Be(("ANALISE_DOCUMENTAL", "Análise documental"));
        TipoDaEtapa(recarregado, provaId).AdmitePontuacao.Should().BeTrue();
    }

    private static TipoEtapaSnapshot TipoDaEtapa(ProcessoSeletivo processo, Guid etapaId) =>
        processo.Etapas.Single(e => e.Id == etapaId).TipoEtapa;

    /// <summary>O caminho do handler quando o caráter da etapa muda e o tipo relido passou a pontuar.</summary>
    private Task RegravarOsSinalizadoresDaAnaliseDocumentalAsync(Guid processoId, Guid etapaId) =>
        ReconfigurarAsync(processoId, carregado =>
        {
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == etapaId);
            tracked.TipoEtapa.AdmitePontuacao.Should().BeFalse();
            tracked.TipoEtapa.AdmiteEliminacao.Should().BeTrue();
            tracked.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: tracked.Nome, Carater: CaraterEtapa.Ambas, TipoEtapa: tracked.TipoEtapa.ComSinalizadores(true, true, false), Ordem: tracked.Ordem, FaseCodigo: null),
                Declaracao(peso: 1m, notaMinima: 5m)).IsSuccess.Should().BeTrue();
        });

    private Task RegravarOsSinalizadoresDaNotaDoEnemAsync(Guid processoId, Guid doEnemId, Guid provaId) =>
        ReconfigurarAsync(processoId, carregado =>
        {
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == doEnemId);
            tracked.DeclaraNotaDoEnem.Should().BeTrue();
            carregado.Etapas.Single(e => e.Id == provaId).DeclaraNotaDoEnem.Should().BeFalse();
            Regravar(tracked, tracked.TipoEtapa.ComSinalizadores(true, false, false), Declaracao(peso: 1m, notaMinima: null));
        });

    private async Task ReconfigurarAsync(Guid processoId, Action<ProcessoSeletivo> reconfigurar)
    {
        await using UniPlusDbContext configureContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext)
            .ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        reconfigurar(carregado);

        await configureContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<ProcessoSeletivo> RecarregarAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        return (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
    }
}
