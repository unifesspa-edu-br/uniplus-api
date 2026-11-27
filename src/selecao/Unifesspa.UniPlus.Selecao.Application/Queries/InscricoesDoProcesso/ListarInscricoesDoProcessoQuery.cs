namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

/// <summary>Lista as inscrições enviadas do processo, paginadas por cursor bidirecional (ADR-0026, ADR-0089).</summary>
public sealed record ListarInscricoesDoProcessoQuery(
    Guid ProcessoSeletivoId,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarInscricoesDoProcessoResult>;

/// <summary>Resultado da <see cref="ListarInscricoesDoProcessoQuery"/>.</summary>
public sealed record ListarInscricoesDoProcessoResult(
    IReadOnlyList<InscricaoDoProcessoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
