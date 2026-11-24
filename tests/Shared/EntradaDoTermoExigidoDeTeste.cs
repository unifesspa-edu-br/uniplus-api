namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;

/// <summary>
/// Monta o termo que o formulário exige na forma de entrada dos testes: sem dizer nada além do código, é um termo de
/// ordem zero, sem versão escolhida, que aparece sempre e é sempre obrigatório. Cada método devolve um construtor novo,
/// então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record EntradaDoTermoExigidoDeTeste
{
    private string Codigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private VersaoDoTermoInput? Termo { get; init; }

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao { get; init; }

    private string? TipoDaObrigatoriedade { get; init; } = TipoObrigatoriedade.Sempre.Codigo();

    private IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoDaObrigatoriedade { get; init; }

    private bool TemRegras { get; init; } = true;

    /// <summary>O termo de código dado, na ordem dada, ainda sem versão escolhida.</summary>
    public static EntradaDoTermoExigidoDeTeste Exigindo(string codigo, int ordem = 0) => new() { Codigo = codigo, Ordem = ordem };

    /// <summary>A versão do catálogo de termos que o formulário exige.</summary>
    public EntradaDoTermoExigidoDeTeste DaVersao(Guid termoId, Guid versaoId) => this with { Termo = new VersaoDoTermoInput(termoId, versaoId) };

    public EntradaDoTermoExigidoDeTeste ExibidoQuando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao) => this with { Exibicao = exibicao };

    /// <summary>O token da obrigatoriedade e, quando é <c>QUANDO</c>, o predicado dela.</summary>
    public EntradaDoTermoExigidoDeTeste ComObrigatoriedade(
        string? tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado = null) =>
        this with { TipoDaObrigatoriedade = tipo, PredicadoDaObrigatoriedade = predicado };

    /// <summary>O termo sem as regras, como o pedido que as omite.</summary>
    public EntradaDoTermoExigidoDeTeste SemRegras() => this with { TemRegras = false };

    public TermoExigidoInput Construir() => new(
        Codigo,
        Ordem,
        Termo,
        TemRegras ? new ExibicaoEObrigatoriedadeInput(Exibicao, new ObrigatoriedadeInput(TipoDaObrigatoriedade, PredicadoDaObrigatoriedade)) : null);
}
