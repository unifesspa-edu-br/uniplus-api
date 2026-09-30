namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista de manutenção do catálogo de fatos, paginada, com filtro opcional pela origem (token
/// canônico) e pelo estado ativo.
/// </summary>
public sealed record ListarFatosCandidatoParaManutencaoQuery(
    Guid? AfterId, int Limit, PaginationDirection Direction, string? Origem, bool? Ativo)
    : IQuery<ListarFatosCandidatoParaManutencaoResult>;
