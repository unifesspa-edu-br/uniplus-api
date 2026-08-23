namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarMotivosDecisaoIsencaoQuery"/>
/// (ADR-0089). As âncoras são os <c>Id</c> de fronteira da janela; o controller
/// emite os cursores cifrados e o header <c>Link</c>.
/// </summary>
public sealed record ListarMotivosDecisaoIsencaoResult(
    IReadOnlyList<MotivoDecisaoIsencaoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
