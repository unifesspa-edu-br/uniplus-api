namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Lista as concessões paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarConcessoesQuery(
    string? PessoaEmissor,
    string? PessoaSub,
    Guid? UnidadeId,
    Guid? PerfilId,
    SituacaoDaConcessao Situacao,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarConcessoesResult>;

/// <summary>Resultado paginado: as âncoras são os <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarConcessoesResult(
    IReadOnlyList<ConcessaoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
