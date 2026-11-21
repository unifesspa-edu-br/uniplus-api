namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Monta o critério de desempate que o pedido recebe: a regra na versão <c>v1</c>, na ordem 1 e sem parâmetros, salvo o
/// que o teste diz. Cada método devolve um construtor novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record CriterioDesempateDeTeste
{
    private int Ordem { get; init; } = 1;

    private ReferenciaRegraInput? Regra { get; init; }

    private Guid? EtapaRef { get; init; }

    private int? IdadeMinima { get; init; }

    private string? Fato { get; init; }

    private string? Operador { get; init; }

    private string? Valor { get; init; }

    private IReadOnlyList<string>? Areas { get; init; }

    private bool ComParametros { get; init; }

    /// <summary>O critério da regra do catálogo na versão <c>v1</c>.</summary>
    public static CriterioDesempateDeTeste DaRegra(string codigo) => new() { Regra = new(codigo, "v1") };

    public CriterioDesempateDeTeste NaOrdem(int ordem) => this with { Ordem = ordem };

    public CriterioDesempateDeTeste NaVersao(string? versao) => this with { Regra = new(Regra?.Codigo, versao) };

    public CriterioDesempateDeTeste SemRegra() => this with { Regra = null };

    public CriterioDesempateDeTeste NaEtapa(Guid etapaRef) => this with { EtapaRef = etapaRef, ComParametros = true };

    public CriterioDesempateDeTeste ComIdadeMinima(int idadeMinima) => this with { IdadeMinima = idadeMinima, ComParametros = true };

    public CriterioDesempateDeTeste ComPredicado(string? fato, string? operador, string? valor) =>
        this with { Fato = fato, Operador = operador, Valor = valor, ComParametros = true };

    public CriterioDesempateDeTeste ComAreas(params string[] areas) => this with { Areas = areas, ComParametros = true };

    public CriterioDesempateInput Construir() =>
        new(Ordem, Regra, ComParametros ? new ParametrosDoCriterioDesempateInput(EtapaRef, IdadeMinima, Fato, Operador, Valor, Areas) : null);
}
