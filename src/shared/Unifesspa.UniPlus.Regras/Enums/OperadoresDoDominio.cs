namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A matriz operador × domínio da ADR-0111: quais operadores uma condição aceita sobre um fato, conforme o
/// <see cref="TipoDominioFato"/> dele. <see cref="Operador.Diferente"/> acompanha <see cref="Operador.Igual"/> em todo
/// domínio onde ele vale, e <see cref="Operador.NaoEm"/> acompanha <see cref="Operador.Em"/>, que só vale nos dois
/// domínios categóricos.
/// </summary>
public static class OperadoresDoDominio
{
    private static readonly Operador[] Booleano = [Operador.Igual, Operador.Diferente];

    private static readonly Operador[] Numerico = [Operador.Igual, Operador.Diferente, Operador.MaiorIgual, Operador.MenorIgual];

    private static readonly Operador[] Categorico = [Operador.Igual, Operador.Diferente, Operador.Em, Operador.NaoEm];

    /// <summary>Os operadores que o domínio admite, na ordem em que a matriz os lista.</summary>
    public static IReadOnlyList<Operador> Admitidos(TipoDominioFato dominio) => dominio switch
    {
        TipoDominioFato.Booleano => Booleano,
        TipoDominioFato.Numerico => Numerico,
        TipoDominioFato.CategoricoEstatico or TipoDominioFato.CategoricoDinamico => Categorico,
        _ => [],
    };

    /// <summary>Se o domínio admite o operador.</summary>
    public static bool Admite(TipoDominioFato dominio, Operador operador) => Admitidos(dominio).Contains(operador);
}
