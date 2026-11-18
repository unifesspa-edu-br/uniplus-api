namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

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

    public Dictionary<string, JsonElement> DerivadosComNomes(IReadOnlyDictionary<string, JsonElement> valores,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        ComOsNomesConferidos(valores, definicoes, (fato, _) => DosDerivados.GetValueOrDefault(fato), SemEnderecosConferidos);

    public Dictionary<string, JsonElement> RespostasComNomes(IReadOnlyDictionary<string, JsonElement> valores,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        ComOsNomesConferidos(valores, definicoes, (_, codigo) => DasRespostas.GetValueOrDefault(codigo), DosEnderecos);

    public Dictionary<string, JsonElement> SubitensComNomes(IReadOnlyDictionary<string, JsonElement> valores,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        ComOsNomesConferidos(valores, definicoes, (_, codigo) => DasRespostas.GetValueOrDefault(codigo), SemEnderecosConferidos);

    private static readonly IReadOnlyDictionary<string, CidadeConferida> SemEnderecosConferidos = new Dictionary<string, CidadeConferida>(StringComparer.Ordinal);

    /// <summary>
    /// A inscrição enviada guarda cada município com o nome que o Geo conferiu, e a cidade de cada endereço conferido pelo CEP
    /// como o Geo a resolveu — código, nome e UF: o que o navegador mandou como cidade é descartado.
    /// </summary>
    /// <param name="nomeDoMunicipio">O nome conferido do município, pelo fato e pelo código; nulo quando ele não foi conferido.</param>
    /// <param name="cidadesDosEnderecos">A cidade que o Geo resolveu pelo CEP de cada endereço conferido, pelo fato do endereço.</param>
    private static Dictionary<string, JsonElement> ComOsNomesConferidos(
        IReadOnlyDictionary<string, JsonElement> valores,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes,
        Func<string, string, string?> nomeDoMunicipio,
        IReadOnlyDictionary<string, CidadeConferida> cidadesDosEnderecos)
        => valores.ToDictionary(static v => v.Key,
            v => ValorComNomeConferido(v.Key, v.Value, definicoes.GetValueOrDefault(v.Key), nomeDoMunicipio, cidadesDosEnderecos), StringComparer.Ordinal);

    private static JsonElement ValorComNomeConferido(string fato, JsonElement valor, DefinicaoDaResposta? definicao,
        Func<string, string, string?> nomeDoMunicipio, IReadOnlyDictionary<string, CidadeConferida> cidadesDosEnderecos) => definicao switch
        {
            { EhMunicipio: true } => MunicipioConferido(fato, valor, nomeDoMunicipio),
            { Tipo: TipoDoValorDeResposta.Endereco } when cidadesDosEnderecos.TryGetValue(fato, out CidadeConferida? cidade)
                && EnderecoDeclarado.TentarLer(valor, out EnderecoDeclarado endereco) =>
                    (endereco with { CidadeCodigoIbge = cidade.CodigoIbge, CidadeNome = cidade.Nome, CidadeUf = cidade.Uf }).ParaJson(),
            _ => valor,
        };

    private static JsonElement MunicipioConferido(string fato, JsonElement valor, Func<string, string, string?> nomeDoMunicipio)
    {
        string codigo = ValorDeMunicipio.Codigo(valor)
            ?? throw new InvalidOperationException($"A resposta de '{fato}' não é um município: o envio a recusa antes de gravar.");
        string nome = nomeDoMunicipio(fato, codigo)
            ?? throw new InvalidOperationException($"O município do campo '{fato}' não foi conferido no Geo: o envio não grava município sem nome conferido.");

        return ValorDeMunicipio.Gravado(codigo, nome);
    }


}

/// <summary>A cidade como o Geo a resolveu pelo CEP: o código IBGE, o nome e a UF.</summary>
public sealed record CidadeConferida(string CodigoIbge, string Nome, string Uf);
