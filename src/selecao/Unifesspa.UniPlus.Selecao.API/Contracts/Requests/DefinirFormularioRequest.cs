namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Application.Commands.ProcessosSeletivos;

using Controllers;

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirFormulario"/> — o processo e a finalidade vêm da
/// rota, e a precondição do header <c>If-Match</c>. A lista de etapas é obrigatória: o formulário
/// termina sempre na revisão e aceite.
/// </summary>
public sealed record DefinirFormularioRequest(
    Guid? FaseId,
    string? Titulo,
    [property: JsonRequired] IReadOnlyList<EtapaFormularioInput> Etapas);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirItens"/>. A lista é obrigatória: só a lista
/// vazia explícita remove os itens, nunca um corpo sem ela.
/// </summary>
public sealed record DefinirItensDoFormularioRequest([property: JsonRequired] IReadOnlyList<FatoColetadoInput> Itens);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirTermos"/>. A lista é obrigatória: só a lista
/// vazia explícita remove os termos, nunca um corpo sem ela.
/// </summary>
public sealed record DefinirTermosDoFormularioRequest([property: JsonRequired] IReadOnlyList<TermoExigidoInput> Termos);
