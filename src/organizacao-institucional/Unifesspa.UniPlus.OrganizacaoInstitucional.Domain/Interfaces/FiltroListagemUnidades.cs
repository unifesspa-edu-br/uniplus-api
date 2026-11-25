namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Critérios opcionais de filtragem da listagem de unidades (issue #640),
/// aplicados no read-side sobre a paginação por cursor keyset (ADR-0026).
/// Ambos os critérios combinam (AND) e são ortogonais ao cursor: como o
/// keyset ordena por <c>Id</c>, a janela permanece coerente sobre o conjunto
/// já filtrado.
/// </summary>
/// <param name="Termo">
/// Termo de busca validado e normalizado; <c>null</c> = sem filtro textual.
/// O repositório prepara o termo e consulta as colunas normalizadas pela plataforma.
/// </param>
/// <param name="Tipos">
/// Tipos de unidade a incluir (OR entre si); vazio = sem filtro por tipo.
/// </param>
public sealed record FiltroListagemUnidades(TermoDeBusca? Termo, IReadOnlyList<TipoUnidade> Tipos)
{
    /// <summary>Filtro sem critérios — equivalente a listar tudo.</summary>
    public static FiltroListagemUnidades Nenhum { get; } = new(null, []);

    /// <summary>Indica se há filtro por tipo aplicável.</summary>
    public bool TemTipos => Tipos.Count > 0;
}
