namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarBasesLegaisDoFadaQuery(
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarBasesLegaisDoFadaResult>;
