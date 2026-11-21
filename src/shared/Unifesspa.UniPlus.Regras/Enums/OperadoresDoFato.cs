namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// A matriz operador × domínio da ADR-0111 e o rótulo de cada operador conforme a cardinalidade do fato: quais
/// operadores uma condição aceita sobre um fato e como a pessoa os lê. <see cref="Operador.Diferente"/> acompanha
/// <see cref="Operador.Igual"/> em todo domínio onde ele vale, e <see cref="Operador.NaoEm"/> acompanha
/// <see cref="Operador.Em"/>, que só vale nos dois domínios categóricos. Só o fato categórico pode ter mais de um valor.
/// </summary>
public static class OperadoresDoFato
{
    private static readonly Operador[] Booleano = [Operador.Igual, Operador.Diferente];

    private static readonly Operador[] Numerico = [Operador.Igual, Operador.Diferente, Operador.MaiorIgual, Operador.MenorIgual];

    private static readonly Operador[] Categorico = [Operador.Igual, Operador.Diferente, Operador.Em, Operador.NaoEm];

    /// <summary>
    /// Os operadores que o domínio admite, na ordem em que a tela os oferece (o primeiro é o padrão da condição nova),
    /// qualquer que seja a cardinalidade.
    /// </summary>
    public static IReadOnlyList<Operador> Admitidos(TipoDominioFato dominio) => dominio switch
    {
        TipoDominioFato.Booleano => Booleano,
        TipoDominioFato.Numerico => Numerico,
        TipoDominioFato.CategoricoEstatico or TipoDominioFato.CategoricoDinamico => Categorico,
        _ => [],
    };

    /// <summary>
    /// Os operadores que uma condição pode usar sobre o fato, na ordem em que a tela os oferece; o primeiro é o
    /// padrão da condição nova. Nenhum quando o fato não é citável em regra (texto, data, endereço) ou quando não
    /// é categórico e declara mais de um valor.
    /// </summary>
    public static IReadOnlyList<Operador> Admitidos(TipoDominioFato dominio, CardinalidadeDoFato cardinalidade) =>
        cardinalidade == CardinalidadeDoFato.Conjunto && !EhCategorico(dominio) ? [] : Admitidos(dominio);

    /// <summary>Se o domínio admite o operador.</summary>
    public static bool Admite(TipoDominioFato dominio, Operador operador) => Admitidos(dominio).Contains(operador);

    /// <summary>O rótulo do operador no sentido que a cardinalidade do fato dá a ele.</summary>
    /// <exception cref="InvalidOperationException">O operador não vale sobre conjunto e não declara rótulo para ele.</exception>
    public static string Rotulo(Operador operador, CardinalidadeDoFato cardinalidade) =>
        cardinalidade == CardinalidadeDoFato.Conjunto
            ? RotulosSobreConjunto.De(operador) ?? throw new InvalidOperationException($"O operador {operador} não vale sobre conjunto.")
            : operador.Rotulo();

    private static bool EhCategorico(TipoDominioFato dominio) =>
        dominio is TipoDominioFato.CategoricoEstatico or TipoDominioFato.CategoricoDinamico;
}
