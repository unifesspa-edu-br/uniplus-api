namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarObrigatoriedadesLegaisQuery"/>.
/// <see cref="ProximoAfterId"/> é o último <c>Id</c> da janela retornada
/// quando houve <c>take</c> itens — controller emite o cursor encriptado
/// + header <c>Link</c> de next.
/// </summary>
public sealed record ListarObrigatoriedadesLegaisResult(
    IReadOnlyList<ObrigatoriedadeLegalDto> Items,
    Guid? ProximoAfterId);
