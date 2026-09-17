namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Domain.Interfaces;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Vitrine pública de certames, ordenada por urgência e paginada por chave.
/// </summary>
/// <param name="Instante">
/// Instante que segmenta abertos e encerrados. Congelado na primeira página e repetido nas
/// seguintes: sem isso, um prazo que vence no meio do percurso moveria o certame de segmento, e ele
/// apareceria duas vezes ou sumiria.
/// </param>
public sealed record ListarCertamesPublicadosQuery(
    DateTimeOffset Instante,
    SituacaoDoCertame Situacao,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarCertamesPublicadosResult>;

/// <summary>Página da vitrine, com as âncoras de continuação.</summary>
public sealed record ListarCertamesPublicadosResult(
    IReadOnlyList<CertameNaVitrineDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
