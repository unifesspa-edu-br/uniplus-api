namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarTiposEtapaQuery(Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<ListarTiposEtapaResult>;
