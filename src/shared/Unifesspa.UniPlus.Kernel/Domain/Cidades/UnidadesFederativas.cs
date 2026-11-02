namespace Unifesspa.UniPlus.Kernel.Domain.Cidades;

using System.Collections.Frozen;

/// <summary>Uma unidade federativa do Brasil.</summary>
/// <param name="Sigla">A sigla de duas letras.</param>
/// <param name="Nome">O nome por extenso.</param>
/// <param name="CodigoIbge">O código IBGE da UF, de dois dígitos, que também prefixa o código IBGE dos municípios dela.</param>
public sealed record UnidadeFederativa(string Sigla, string Nome, string CodigoIbge);

/// <summary>
/// A lista fixa das 27 unidades federativas, com sigla, nome e código IBGE. É a fonte do nome com que a UF é exibida,
/// venha o valor como sigla ou como código IBGE, e da coerência entre a UF e o prefixo do código do município.
/// </summary>
public static class UnidadesFederativas
{
    private static readonly FrozenDictionary<string, UnidadeFederativa> PorSiglaDaUf;
    private static readonly FrozenDictionary<string, UnidadeFederativa> PorCodigo;

    static UnidadesFederativas()
    {
        Todas =
        [
            new("RO", "Rondônia", "11"),
            new("AC", "Acre", "12"),
            new("AM", "Amazonas", "13"),
            new("RR", "Roraima", "14"),
            new("PA", "Pará", "15"),
            new("AP", "Amapá", "16"),
            new("TO", "Tocantins", "17"),
            new("MA", "Maranhão", "21"),
            new("PI", "Piauí", "22"),
            new("CE", "Ceará", "23"),
            new("RN", "Rio Grande do Norte", "24"),
            new("PB", "Paraíba", "25"),
            new("PE", "Pernambuco", "26"),
            new("AL", "Alagoas", "27"),
            new("SE", "Sergipe", "28"),
            new("BA", "Bahia", "29"),
            new("MG", "Minas Gerais", "31"),
            new("ES", "Espírito Santo", "32"),
            new("RJ", "Rio de Janeiro", "33"),
            new("SP", "São Paulo", "35"),
            new("PR", "Paraná", "41"),
            new("SC", "Santa Catarina", "42"),
            new("RS", "Rio Grande do Sul", "43"),
            new("MS", "Mato Grosso do Sul", "50"),
            new("MT", "Mato Grosso", "51"),
            new("GO", "Goiás", "52"),
            new("DF", "Distrito Federal", "53"),
        ];
        PorSiglaDaUf = Todas.ToFrozenDictionary(static uf => uf.Sigla, StringComparer.Ordinal);
        PorCodigo = Todas.ToFrozenDictionary(static uf => uf.CodigoIbge, StringComparer.Ordinal);
    }

    /// <summary>As 27 unidades federativas, em ordem do código IBGE.</summary>
    public static IReadOnlyList<UnidadeFederativa> Todas { get; }

    /// <summary>A UF da sigla (comparação exata, case-sensitive), ou nula quando não é uma das 27.</summary>
    public static UnidadeFederativa? PorSigla(string? sigla) =>
        sigla is not null && PorSiglaDaUf.TryGetValue(sigla.Trim(), out UnidadeFederativa? uf) ? uf : null;

    /// <summary>A UF do código IBGE de dois dígitos, ou nula quando não é de uma das 27.</summary>
    public static UnidadeFederativa? PorCodigoIbge(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo.Trim(), out UnidadeFederativa? uf) ? uf : null;
}
