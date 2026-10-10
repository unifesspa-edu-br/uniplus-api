namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Collections.Frozen;

/// <summary>
/// Os valores que uma condição pode citar para um fato categórico de domínio dinâmico: um
/// conjunto que o processo oferece (modalidades, opções declaradas, municípios do bônus) ou uma
/// regra de formato, quando o conjunto vem de uma fonte que o servidor não enumera, como os
/// municípios do Geo (referência fraca por formato, ADR-0090).
/// </summary>
/// <remarks>
/// O domínio diz também o que a recusa do valor fora dele orienta: o que o processo oferece se
/// oferta no processo; o que vem de outra fonte só se corrige na condição.
/// </remarks>
public sealed class DominioDeValores
{
    private const string ValorNaoAdmitido = "não é um valor admitido para o fato.";

    private const string ValorNaoOfertado =
        "não está entre os valores que o processo oferece para o fato; oferte o valor no processo ou ajuste a condição.";

    private readonly Func<string, bool> _contem;

    private DominioDeValores(IReadOnlySet<string>? valores, Func<string, bool> contem, string foraDoDominio)
    {
        Valores = valores;
        _contem = contem;
        ForaDoDominio = foraDoDominio;
    }

    /// <summary>Os valores do domínio, quando ele é enumerado; <see langword="null"/> no domínio por formato.</summary>
    public IReadOnlySet<string>? Valores { get; }

    /// <summary>
    /// O que a recusa diz do valor fora do domínio, depois de nomear o valor e o fato da condição.
    /// </summary>
    public string ForaDoDominio { get; }

    /// <summary>Um domínio com os valores dados, comparados de forma ordinal.</summary>
    public static DominioDeValores Enumerado(IEnumerable<string> valores) => Enumerado(valores, ValorNaoAdmitido);

    /// <summary>
    /// Os valores que o processo oferece para o fato, comparados de forma ordinal: a recusa do valor
    /// fora deles orienta ofertá-lo no processo.
    /// </summary>
    public static DominioDeValores OfertadoPeloProcesso(IEnumerable<string> valores) => Enumerado(valores, ValorNaoOfertado);

    /// <summary>Um domínio definido por uma regra de formato, sem enumeração.</summary>
    public static DominioDeValores PorFormato(Func<string, bool> regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return new DominioDeValores(null, regra, ValorNaoAdmitido);
    }

    public bool Contem(string valor) => _contem(valor);

    private static DominioDeValores Enumerado(IEnumerable<string> valores, string foraDoDominio)
    {
        ArgumentNullException.ThrowIfNull(valores);
        FrozenSet<string> conjunto = valores.ToFrozenSet(StringComparer.Ordinal);
        return new DominioDeValores(conjunto, conjunto.Contains, foraDoDominio);
    }
}
