namespace Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarProcessosAcompanhamentoQuery(Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<ListarProcessosAcompanhamentoResult>;
