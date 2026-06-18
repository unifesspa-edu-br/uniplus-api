namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

/// <summary>
/// Critérios opcionais da listagem de cidades (<c>GET /api/cidades?uf=&amp;q=</c>),
/// aplicados no read-side sobre a paginação por cursor keyset. Ambos combinam (AND)
/// e são ortogonais ao cursor: como o keyset ordena por <c>Id</c>, a janela
/// permanece coerente sobre o conjunto já filtrado.
/// </summary>
/// <param name="Uf">
/// UF do filtro (<c>case-insensitive</c>, normalizada para maiúsculas no reader);
/// <see langword="null"/>/vazio = sem filtro por UF. UF inexistente resulta em
/// lista vazia (não é erro — é filtro, não recurso).
/// </param>
/// <param name="Termo">
/// Termo de busca bruto do usuário; <see langword="null"/>/vazio = sem filtro
/// textual. A normalização (remoção de diacríticos) e o <c>ILIKE</c> sobre
/// <c>nome_normalizado</c> são aplicados no reader.
/// </param>
public sealed record FiltroListagemCidades(string? Uf, string? Termo)
{
    /// <summary>Filtro sem critérios — equivalente a listar tudo (vigente).</summary>
    public static FiltroListagemCidades Nenhum { get; } = new(null, null);

    /// <summary>Indica se há filtro por UF aplicável.</summary>
    public bool TemUf => !string.IsNullOrWhiteSpace(Uf);

    /// <summary>Indica se há termo de busca textual aplicável.</summary>
    public bool TemBusca => !string.IsNullOrWhiteSpace(Termo);
}
