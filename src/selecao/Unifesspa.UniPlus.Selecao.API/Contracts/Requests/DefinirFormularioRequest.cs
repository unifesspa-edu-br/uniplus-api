namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Application.Commands.ProcessosSeletivos;

using Controllers;

/// <summary>
/// Corpo de <see cref="FormularioInscricaoController.DefinirFormulario"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota) e a precondição (vem do header <c>If-Match</c>), mesmo
/// padrão de <see cref="DefinirClassificacaoRequest"/>.
/// </summary>
public sealed record DefinirFormularioRequest(string? Titulo);

/// <summary>
/// Corpo de <see cref="FormularioInscricaoController.DefinirTermos"/>: os termos que o formulário
/// exige, com o mesmo padrão de rota e precondição de <see cref="DefinirFormularioRequest"/>. A lista
/// é obrigatória: só a lista vazia explícita remove os termos, nunca um corpo sem ela.
/// </summary>
public sealed record DefinirTermosDoFormularioRequest([property: JsonRequired] IReadOnlyList<TermoExigidoInput> Termos);
