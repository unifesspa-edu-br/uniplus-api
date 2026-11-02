namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os nomes de município que o Geo conferiu no envio, por onde cada um entra na inscrição. O nome gravado vem sempre do
/// Geo, nunca do navegador: o nome que o navegador mandou na cidade do endereço não é lido.
/// </summary>
/// <param name="DasRespostas">O nome do município de cada resposta de tipo município, no nível superior e nos grupos, pelo código IBGE.</param>
/// <param name="DosEnderecos">A cidade que o Geo resolveu pelo CEP de cada endereço conferido sem recusa, pelo código do fato do endereço.</param>
/// <param name="DosDerivados">O nome do município de cada derivado de tipo município, pelo código do fato derivado.</param>
public sealed record MunicipiosConferidos(
    IReadOnlyDictionary<string, string> DasRespostas,
    IReadOnlyDictionary<string, CidadeConferida> DosEnderecos,
    IReadOnlyDictionary<string, string> DosDerivados)
{
    /// <summary>Nenhum nome conferido: a inscrição sem município nem endereço conferido pelo CEP.</summary>
    public static MunicipiosConferidos Nenhum { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, CidadeConferida>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal));
}

/// <summary>A cidade como o Geo a resolveu pelo CEP: o código IBGE, o nome e a UF.</summary>
public sealed record CidadeConferida(string CodigoIbge, string Nome, string Uf);
