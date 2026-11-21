namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;

/// <summary>
/// Monta o item do formulário na forma de entrada dos testes: sem dizer nada além do fato e da ordem, é um
/// campo booleano de rótulo igual ao código, sempre obrigatório, sem exibição, restrição nem impedimento.
/// Cada método devolve um construtor novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record EntradaDoFatoColetadoDeTeste
{
    private string FatoCodigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private string? Secao { get; init; }

    private string? Rotulo { get; init; }

    private string? Tipo { get; init; } = "BOOLEANO";

    private string? Ajuda { get; init; }

    private bool Confirmacao { get; init; }

    private bool TemApresentacao { get; init; } = true;

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao { get; init; }

    private string? TipoDaObrigatoriedade { get; init; } = PredicadoDnfJson.ObrigatoriedadeSempre;

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoDaObrigatoriedade { get; init; }

    private IReadOnlyList<RestricaoValorInput>? Restricoes { get; init; }

    private ImpedimentoInput? Impedimento { get; init; }

    private bool TemRegras { get; init; } = true;

    /// <summary>O item do fato na ordem dada, com o rótulo igual ao código do fato até o teste dar outro.</summary>
    public static EntradaDoFatoColetadoDeTeste Coletando(string fatoCodigo, int ordem = 0) => new() { FatoCodigo = fatoCodigo, Ordem = ordem };

    public EntradaDoFatoColetadoDeTeste ComRotulo(string? rotulo) => this with { Rotulo = rotulo };

    /// <summary>O código do tipo de campo, como o pedido o informa; nulo é o tipo ausente.</summary>
    public EntradaDoFatoColetadoDeTeste DoTipo(string? tipoRenderizacao) => this with { Tipo = tipoRenderizacao };

    public EntradaDoFatoColetadoDeTeste ComAjuda(string? ajuda) => this with { Ajuda = ajuda };

    public EntradaDoFatoColetadoDeTeste PedindoConfirmacao(bool pedir = true) => this with { Confirmacao = pedir };

    public EntradaDoFatoColetadoDeTeste ExibidoQuando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao) => this with { Exibicao = exibicao };

    /// <summary>O token da obrigatoriedade e, quando é <c>QUANDO</c>, o predicado dela.</summary>
    public EntradaDoFatoColetadoDeTeste ComObrigatoriedade(
        string? tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado = null) =>
        this with { TipoDaObrigatoriedade = tipo, PredicadoDaObrigatoriedade = predicado };

    public EntradaDoFatoColetadoDeTeste ComRestricoes(params IReadOnlyList<RestricaoValorInput>? restricoes) => this with { Restricoes = restricoes };

    public EntradaDoFatoColetadoDeTeste ComImpedimento(ImpedimentoInput? impedimento) => this with { Impedimento = impedimento };

    public EntradaDoFatoColetadoDeTeste NaSecao(string? secao) => this with { Secao = secao };

    /// <summary>O item sem a apresentação, como o pedido que a omite.</summary>
    public EntradaDoFatoColetadoDeTeste SemApresentacao() => this with { TemApresentacao = false };

    /// <summary>O item sem as regras, como o pedido que as omite.</summary>
    public EntradaDoFatoColetadoDeTeste SemRegras() => this with { TemRegras = false };

    public FatoColetadoInput Construir() => new(
        FatoCodigo,
        Ordem,
        TemApresentacao ? new ApresentacaoDoCampoInput(Rotulo ?? FatoCodigo, Tipo, Ajuda, Confirmacao) : null,
        TemRegras ? new RegrasDoCampoInput(Exibicao, new ObrigatoriedadeInput(TipoDaObrigatoriedade, PredicadoDaObrigatoriedade), Restricoes, Impedimento) : null,
        Secao);
}
