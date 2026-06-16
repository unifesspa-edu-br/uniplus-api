namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarObrigatoriedadesLegaisQuery"/>
/// (ADR-0089). As âncoras <c>prev</c>/<c>next</c> são os <c>Id</c> de fronteira
/// da janela; o controller emite os cursores cifrados + header <c>Link</c>.
/// </summary>
public sealed record ListarObrigatoriedadesLegaisResult(
    IReadOnlyList<ObrigatoriedadeLegalDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
