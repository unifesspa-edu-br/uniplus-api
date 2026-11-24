namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O que um teste declara de uma etapa; o que não declara tem o valor neutro de uma etapa de prova objetiva conforme.</summary>
internal sealed record ModeloDeEtapa(string Nome)
{
    internal Guid Id { get; init; } = Guid.CreateVersion7();

    internal int? Ordem { get; init; } = 1;

    internal string? FaseCodigo { get; init; }

    internal CaraterEtapa Carater { get; init; } = CaraterEtapa.Classificatoria;

    internal bool NotaDoEnem { get; init; }

    internal bool TipoAdmitePontuacao { get; init; } = true;

    internal bool TipoAdmiteEliminacao { get; init; } = true;

    internal decimal? Peso { get; init; } = 1m;

    internal decimal? NotaMinima { get; init; }

    internal decimal? NotaMaxima { get; init; } = 10m;

    internal decimal? Passo { get; init; }

    internal FormaDeAvaliacao? Forma { get; init; } = FormaDeAvaliacao.Objetiva;

    internal ReferenciaRegra? RegraConsolidacao { get; init; }

    internal IReadOnlyList<ItemDeAvaliacaoDeclarado> Itens { get; init; } = [];

    internal IReadOnlyList<NivelDeAtendimento?>? Escala { get; init; }

    internal ConceitoDaEtapa? Conceito { get; init; }

    internal DateTimeOffset? Inicio { get; init; }

    internal DateTimeOffset? Fim { get; init; }

    internal bool EmiteParecerIndividual { get; init; }

    internal IReadOnlyList<ProdutoDaEtapa> Produtos { get; init; } = [];

    internal IReadOnlyList<RecursoDaEtapa> Recursos { get; init; } = [];

    internal IReadOnlyList<BancaDaEtapa> Bancas { get; init; } = [];

    internal IReadOnlyList<string?>? TiposDeDecisao { get; init; }

    internal ModeloDeEtapa Humana() => this with
    {
        Forma = FormaDeAvaliacao.Humana,
        RegraConsolidacao = ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", new string('a', 64)),
        Passo = 0.5m,
        Bancas = [BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), BancasDeTeste.Regra())],
    };

    internal ModeloDeEtapa SemNota() => this with
    {
        Carater = CaraterEtapa.Eliminatoria,
        Peso = null,
        NotaMaxima = null,
        Forma = null,
    };

    internal ModeloDeEtapa DoEnem() => this with { NotaDoEnem = true, NotaMaxima = null, Forma = null };

    internal ModeloDeEtapa Categorica() => this with
    {
        Carater = CaraterEtapa.Eliminatoria,
        Peso = null,
        NotaMaxima = null,
        Forma = null,
        Conceito = AptoInapto(),
    };

    internal EtapaDeclarada Declarar() => EtapaDeclarada.Declarar(
        Id,
        Identificacao(),
        new AvaliacaoDeclaradaDaEtapa(Avaliacao(), Itens, Conceito),
        new JanelaDeTempo(Inicio, Fim),
        new PublicacoesDaEtapa(Produtos, EmiteParecerIndividual, Recursos, TiposDeDecisao),
        Bancas);

    /// <summary>A etapa criada pela fábrica e preenchida pelos mesmos comandos da raiz; o conceito não faz parte da entidade.</summary>
    internal EtapaProcesso Criar()
    {
        Result<EtapaProcesso> criada = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, Identificacao(), Avaliacao(), Itens);
        EtapaProcesso etapa = criada.IsSuccess
            ? criada.Value!
            : throw new InvalidOperationException($"O modelo de etapa declara o que a etapa recusa: {string.Join(", ", criada.Errors.Select(static e => e.Error.Code))}.");
        Aceitar(etapa.DefinirJanelaEParecer(Inicio, Fim, EmiteParecerIndividual));
        Aceitar(etapa.DefinirProdutos(Produtos));
        etapa.DefinirBancas(Bancas);
        Aceitar(etapa.DefinirRecursos(Recursos));
        Aceitar(etapa.DefinirTiposDeDecisao(TiposDeDecisao));

        return etapa;
    }

    internal static ConceitoDaEtapa AptoInapto() =>
        ConceitoDaEtapa.Criar(
            "APTO_INAPTO", "Apto ou inapto", FormaDoConceito.PorOpcoes, null,
            [new ValorCategoricoInformado("APTO", "Apto", true), new ValorCategoricoInformado("INAPTO", "Inapto", false)]).Value!;

    internal static ConceitoDaEtapa NotaDeZeroADez() =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", FormaDoConceito.Numerico, new EscalaInformada(0m, 10m, 2), null).Value!;

    internal static ProdutoDaEtapa Preliminar(string ato) => ProdutoDaEtapa.Criar(ato, PapelDaPublicacaoPrevista.Preliminar);

    internal static ProdutoDaEtapa Definitivo(string ato) => ProdutoDaEtapa.Criar(ato, PapelDaPublicacaoPrevista.Definitivo);

    private static void Aceitar(Result resultado)
    {
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException("O modelo de etapa declara o que a etapa recusa.");
        }
    }

    internal IdentificacaoDaEtapa Identificacao() => new(Nome, Carater, Tipo(), Ordem, FaseCodigo);

    internal DeclaracaoDaAvaliacaoDaEtapa Avaliacao() => new(Peso, NotaMinima, NotaMaxima, Passo, Forma, RegraConsolidacao, Escala);

    private TipoEtapaSnapshot Tipo() => TipoEtapaSnapshot.Criar(
        Guid.CreateVersion7(), "PROVA", "Prova", TipoAdmitePontuacao, TipoAdmiteEliminacao, notaDeOrigemNoEnem: NotaDoEnem).Value!;
}
