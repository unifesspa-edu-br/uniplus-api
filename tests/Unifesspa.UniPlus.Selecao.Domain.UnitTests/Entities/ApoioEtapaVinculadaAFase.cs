namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O certame, as fases e as etapas que os testes do vínculo da etapa com a fase montam.</summary>
internal static class ApoioEtapaVinculadaAFase
{
    internal static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise Documental", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    internal static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>
    /// Fase do cronograma, com ou sem janela. A origem da data acompanha o que foi declarado
    /// porque é ela que decide se a janela é obrigatória: só a fase com os dois extremos pode
    /// ser NO_EDITAL, e é NA_EXECUCAO que torna a janela meia ou ausente exprimível.
    /// </summary>
    internal static FaseCronograma Fase(
        int ordem, string codigo, DateTimeOffset? inicio = null, DateTimeOffset? fim = null) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, inicio is not null && fim is not null ? OrigemDataFase.NoEdital : OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false,
                ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: inicio, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    internal static EtapaProcesso Etapa(string nome, string? faseCodigo, int ordem = 1) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: Tipo(), Ordem: ordem, FaseCodigo: faseCodigo),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    internal static DateTimeOffset Instante(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    internal static DateTimeOffset? InstanteOpcional(string? iso) =>
        iso is null ? null : Instante(iso);

    /// <summary>Certame com uma fase AVALIACAO de 1 a 10 de março de 2027.</summary>
    internal static ProcessoSeletivo ProcessoComFaseDeMarco()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", Instante("2027-03-01T00:00:00Z"), Instante("2027-03-10T00:00:00Z"))],
            []).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>
    /// Etapa com janela própria, que pode ser meia: declarar só um dos extremos é estado
    /// válido — o outro a etapa herda da fase.
    /// </summary>
    internal static EtapaProcesso EtapaComJanela(
        string nome, string? faseCodigo, DateTimeOffset? inicio, DateTimeOffset? fim, int ordem = 1)
    {
        EtapaProcesso etapa = Etapa(nome, faseCodigo, ordem);
        etapa.DefinirJanelaEParecer(inicio, fim, emiteParecerIndividual: false).IsSuccess.Should().BeTrue();
        return etapa;
    }

    internal static FaseCronograma FaseDeAvaliacao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "AVALIACAO", OrigemDataFase.NaExecucao, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;
}
