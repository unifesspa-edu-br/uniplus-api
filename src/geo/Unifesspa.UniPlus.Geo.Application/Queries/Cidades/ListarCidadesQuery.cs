namespace Unifesspa.UniPlus.Geo.Application.Queries.Cidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista as Cidades vigentes paginadas por cursor bidirecional (ADR-0026 +
/// ADR-0089), com filtro opcional por <paramref name="Uf"/> e busca textual
/// <paramref name="Busca"/> (acento/caixa-insensível). Os parâmetros já chegam
/// decodificados — o controller decifra o cursor no boundary (ADR-0031).
/// </summary>
/// <param name="AfterSortKey">Chave de ordenação (nome) da âncora; par com <paramref name="AfterId"/> (ADR-0094).</param>
/// <param name="AfterId">Id de desempate da âncora; <see langword="null"/> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>).</param>
/// <param name="Uf">Filtro por UF; <see langword="null"/>/vazio = sem filtro.</param>
/// <param name="Busca">Termo de busca sobre o nome; <see langword="null"/>/vazio = sem filtro.</param>
public sealed record ListarCidadesQuery(
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Uf,
    string? Busca) : IQuery<ListarCidadesResult>;
