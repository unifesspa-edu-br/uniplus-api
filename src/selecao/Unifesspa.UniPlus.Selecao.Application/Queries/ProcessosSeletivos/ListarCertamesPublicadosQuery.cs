namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Domain.Interfaces;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Vitrine pública de certames, ordenada por urgência e paginada por chave.
/// </summary>
/// <param name="Instante">
/// Instante que segmenta abertos e encerrados. Congelado na primeira página e repetido nas
/// seguintes: sem isso, um prazo que vence no meio do percurso moveria o certame de segmento, e ele
/// apareceria duas vezes ou sumiria.
/// </param>
/// <param name="Recorte">O que reduz a vitrine: situação da janela, modalidade e texto pesquisado.</param>
/// <param name="Ordenacao">
/// Campos pedidos pela consulta, na ordem de prioridade. Vazio é a ordem canônica por urgência.
/// </param>
public sealed record ListarCertamesPublicadosQuery(
    DateTimeOffset Instante,
    RecorteDaVitrine Recorte,
    IReadOnlyList<SortField> Ordenacao,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    bool IncluirContadores) : IQuery<Result<ListarCertamesPublicadosResult>>;

/// <summary>Página da vitrine, com as âncoras de continuação.</summary>
public sealed record ListarCertamesPublicadosResult(
    IReadOnlyList<CertameNaVitrineDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo,
    ContadoresDaVitrine? Contadores);
