namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarBaseLegalBonusRegionalQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarBaseLegalBonusRegionalResult>;
