namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Query paginada por cursor bidirecional (ADR-0026 + ADR-0089) com filtros
/// admin. <c>Vigentes</c> default <see langword="true"/> — leitura pública
/// padrão; admin pode passar <see langword="false"/> para incluir regras com
/// <c>VigenciaFim</c> passada.
/// </summary>
public sealed record ListarObrigatoriedadesLegaisQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? TipoEditalCodigo,
    CategoriaObrigatoriedade? Categoria,
    bool Vigentes) : IQuery<ListarObrigatoriedadesLegaisResult>;
