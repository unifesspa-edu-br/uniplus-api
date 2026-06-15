namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Critérios opcionais de filtragem da listagem de unidades (issue #640),
/// aplicados no read-side sobre a paginação por cursor keyset (ADR-0026).
/// Ambos os critérios combinam (AND) e são ortogonais ao cursor: como o
/// keyset ordena por <c>Id</c>, a janela permanece coerente sobre o conjunto
/// já filtrado.
/// </summary>
/// <param name="Termo">
/// Termo de busca bruto do usuário; <c>null</c> ou vazio = sem filtro textual.
/// A normalização (remoção de diacríticos e caixa) é aplicada server-side pela
/// função <c>immutable_unaccent</c> + <c>ILIKE</c> no repositório.
/// </param>
/// <param name="Tipos">
/// Tipos de unidade a incluir (OR entre si); vazio = sem filtro por tipo.
/// </param>
public sealed record FiltroListagemUnidades(string? Termo, IReadOnlyList<TipoUnidade> Tipos)
{
    /// <summary>Filtro sem critérios — equivalente a listar tudo.</summary>
    public static FiltroListagemUnidades Nenhum { get; } = new(null, []);

    /// <summary>Indica se há termo de busca textual aplicável.</summary>
    public bool TemBusca => !string.IsNullOrWhiteSpace(Termo);

    /// <summary>Indica se há filtro por tipo aplicável.</summary>
    public bool TemTipos => Tipos.Count > 0;
}
