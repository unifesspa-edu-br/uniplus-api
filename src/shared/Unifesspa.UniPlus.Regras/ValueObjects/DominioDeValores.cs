namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Collections.Frozen;

/// <summary>
/// Os valores que uma condição pode citar para um fato categórico de domínio dinâmico: um
/// conjunto que o processo oferece (modalidades, opções declaradas, municípios do bônus) ou uma
/// regra de formato, quando o conjunto vem de uma fonte que o servidor não enumera, como os
/// municípios do Geo (referência fraca por formato, ADR-0090).
/// </summary>
public sealed class DominioDeValores
{
    private readonly Func<string, bool> _contem;

    private DominioDeValores(IReadOnlySet<string>? valores, Func<string, bool> contem)
    {
        Valores = valores;
        _contem = contem;
    }

    /// <summary>Os valores do domínio, quando ele é enumerado; <see langword="null"/> no domínio por formato.</summary>
    public IReadOnlySet<string>? Valores { get; }

    /// <summary>Um domínio com os valores dados, comparados de forma ordinal.</summary>
    public static DominioDeValores Enumerado(IEnumerable<string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        FrozenSet<string> conjunto = valores.ToFrozenSet(StringComparer.Ordinal);
        return new DominioDeValores(conjunto, conjunto.Contains);
    }

    /// <summary>Um domínio definido por uma regra de formato, sem enumeração.</summary>
    public static DominioDeValores PorFormato(Func<string, bool> regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return new DominioDeValores(null, regra);
    }

    public bool Contem(string valor) => _contem(valor);
}
