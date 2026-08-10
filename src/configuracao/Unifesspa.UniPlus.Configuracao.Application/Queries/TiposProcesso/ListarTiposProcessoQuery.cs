namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarTiposProcessoQuery(Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<ListarTiposProcessoResult>;
