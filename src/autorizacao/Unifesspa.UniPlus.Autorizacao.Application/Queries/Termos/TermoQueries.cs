namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Termos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A versão vigente do termo de responsabilidade, que a tela mostra a quem vai pedir.</summary>
public sealed record ObterTermoVigenteQuery : IQuery<Result<VersaoDoTermoDto>>;

/// <summary>Uma versão do termo pelo id: a fixada no pedido, que a tela de aceite mostra ao beneficiário.</summary>
public sealed record ObterVersaoDoTermoQuery(Guid Id) : IQuery<Result<VersaoDoTermoDto>>;

/// <summary>O histórico das versões, paginado por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarVersoesDoTermoQuery(string? AfterSortKey, Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<Result<ListarVersoesDoTermoResult>>;

/// <summary>Resultado paginado: as âncoras são o número da versão e o <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarVersoesDoTermoResult(
    IReadOnlyList<VersaoDoTermoResumoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
