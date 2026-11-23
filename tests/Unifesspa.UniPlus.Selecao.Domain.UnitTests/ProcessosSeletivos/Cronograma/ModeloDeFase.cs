namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O que um teste declara de uma fase; o que não declara tem o valor neutro de uma fase conforme.</summary>
internal sealed record ModeloDeFase(string Codigo, int Ordem)
{
    internal static readonly DateTimeOffset Abertura = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    internal Guid OrigemId { get; init; } = Guid.CreateVersion7();

    internal string Nome { get; init; } = "Fase de avaliação";

    internal DateTimeOffset? Inicio { get; init; }

    internal DateTimeOffset? Fim { get; init; }

    internal OrigemDataFase OrigemData { get; init; } = OrigemDataFase.NaExecucao;

    internal bool ComUnidade { get; init; } = true;

    internal UnidadeResponsavelSnapshot UnidadeResponsavel { get; init; } = UnidadesDeTeste.Ceps;

    internal bool ColetaInscricao { get; init; }

    internal bool ColetaSolicitacaoIsencao { get; init; }

    internal bool AgrupaEtapas { get; init; }

    internal bool NoMesmoMomentoDaAnterior { get; init; }

    internal bool EmiteParecerIndividual { get; init; }

    internal IReadOnlyList<ProdutoDaFase> Produtos { get; init; } = [];

    internal RegraRecursoFase? RegraRecurso { get; init; }

    internal IReadOnlyList<BancaRequerida> Bancas { get; init; } = [];

    internal IReadOnlyList<string?>? TiposDeDecisao { get; init; }

    internal RegraDeHomologacaoDasInscricoes? RegraDeHomologacao { get; init; }

    internal ModeloDeFase Janela(int diaDeAbertura, int diaDeEncerramento) =>
        this with { Inicio = Abertura.AddDays(diaDeAbertura), Fim = Abertura.AddDays(diaDeEncerramento) };

    /// <summary>A fase marcada para acontecer no mesmo momento da fase que a antecede na lista.</summary>
    internal ModeloDeFase NoMesmoMomento() => this with { NoMesmoMomentoDaAnterior = true };

    internal ModeloDeFase Publicando(params ProdutoDaFase[] produtos) => this with { Produtos = produtos };

    /// <summary>A fase que declara o tipo de decisão da homologação das inscrições e a regra dada.</summary>
    internal ModeloDeFase Homologando(RegraDeHomologacaoDasInscricoes regra) =>
        this with { RegraDeHomologacao = regra, TiposDeDecisao = HomologacaoDasInscricoesDeTeste.TiposDeDecisao };

    internal FaseDeclarada Declarar() => FaseDeclarada.Declarar(Guid.CreateVersion7(), Origem(), Decisoes(), Publicacoes(), Bancas);

    internal FaseCronograma Criar() => FaseCronograma.Criar(Origem(), Decisoes(), Publicacoes(), Bancas).Value!;

    internal OrigemCanonicaDaFase Origem() => new(
        OrigemId, Codigo, OrigemData, AgrupaEtapas, PermiteComplementacao: false, ColetaInscricao, ColetaSolicitacaoIsencao)
    { Nome = Nome };

    internal DecisoesDeCronogramaDaFase Decisoes() =>
        new(Ordem, Inicio, Fim, ComUnidade ? UnidadeResponsavel : null) { NoMesmoMomentoDaAnterior = NoMesmoMomentoDaAnterior };

    internal PublicacoesDaFase Publicacoes() => new(Produtos, EmiteParecerIndividual, RegraRecurso, TiposDeDecisao, RegraDeHomologacao);

    internal static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    internal static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    internal static RegraRecursoFase RecursoAncoradoEm(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoAncoraId).Value!;

    internal static CronogramaDoProcesso Cronograma(params FaseDeclarada[] fases) => new(fases);
}
