namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista, paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089), as tentativas de quem confirmou sem ter
/// instalação aguardando código: o que a trilha de uma instalação não alcança.
/// </summary>
public sealed record ListarTentativasSemPendenciaQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarTentativasSemPendenciaResult>;

/// <summary>Resultado paginado: as âncoras são os <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarTentativasSemPendenciaResult(
    IReadOnlyList<TentativaDeInstalacaoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
