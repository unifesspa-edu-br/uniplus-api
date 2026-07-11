namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Todos os atos publicados que tratam de uma entidade — a consulta unificada que é o
/// propósito do módulo (ADR-0105). Ordenada pela data de publicação, com o <c>Id</c>
/// (Guid v7) de desempate: a retificação republica a mesma data, e a data sozinha não
/// ordena de forma estável.
/// </summary>
/// <remarks>
/// <para>O par <c>(EntidadeTipo, EntidadeId)</c> é <b>opaco</b>: Publicações não sabe se
/// a entidade existe — não conhece os domínios, e perguntar-lhes derrubaria a fronteira
/// que este módulo existe para manter. Entidade inexistente e entidade sem ato algum
/// respondem, ambas, uma coleção vazia; nunca 404.</para>
/// <para>Paginação por cursor opaco (ADR-0026) com keyset ordenado (ADR-0094): a âncora é
/// o par <c>(data de publicação, Id)</c>, e o cursor é escopado à entidade — o de um
/// certame não navega a coleção de outro.</para>
/// </remarks>
public sealed record ListarAtosDaEntidadeQuery(
    string EntidadeTipo,
    Guid EntidadeId,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarAtosDaEntidadeResult>;
