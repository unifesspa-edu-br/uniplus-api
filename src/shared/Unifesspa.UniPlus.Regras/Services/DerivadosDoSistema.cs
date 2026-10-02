namespace Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Os fatos que o sistema calcula de atributos do candidato e os fatos de que cada um depende
/// (ADR-0136, UNI-REQ-0075): a faixa etária, da data de nascimento; a UF e o município de
/// residência, do endereço residencial. As dependências são do mecanismo que calcula o fato, e não
/// do administrador; o catálogo as registra pelo seed. A renda per capita fica de fora: depende da
/// renda dos membros da família, que ainda não é fato do catálogo, e por isso não é citável em
/// regra do formulário.
/// </summary>
public static class DerivadosDoSistema
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencias { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["FAIXA_ETARIA"] = ["DATA_NASCIMENTO"],
            ["UF_RESIDENCIA"] = ["ENDERECO_RESIDENCIAL"],
            ["MUNICIPIO_RESIDENCIA"] = ["ENDERECO_RESIDENCIAL"],
        };

    /// <summary>
    /// As derivações que o grafo do formulário confere: as dadas, por regra, e as dos derivados do
    /// sistema, conhecidos quando as dependências deles são.
    /// </summary>
    public static Dictionary<string, IReadOnlyCollection<string>> ComAsDoSistema(
        IEnumerable<KeyValuePair<string, IReadOnlyCollection<string>>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(derivacoes);
        Dictionary<string, IReadOnlyCollection<string>> todas = new(derivacoes, StringComparer.Ordinal);
        foreach ((string derivado, IReadOnlyList<string> dependencias) in Dependencias)
        {
            todas.TryAdd(derivado, dependencias);
        }

        return todas;
    }
}
