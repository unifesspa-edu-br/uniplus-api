namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarTiposConceitoQuery(Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<ListarTiposConceitoResult>;
