namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Lista as concessões paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarConcessoesQuery(
    string? PessoaEmissor,
    string? PessoaContaId,
    Guid? UnidadeId,
    Guid? PerfilId,
    SituacaoDaConcessao? Situacao,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarConcessoesResult>;

/// <summary>Resultado paginado: os pontos de continuação são os <c>Id</c> de fronteira da página.</summary>
public sealed record ListarConcessoesResult(
    IReadOnlyList<ConcessaoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
