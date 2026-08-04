namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="FormularioInscricaoController.DefinirFormulario"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota) e a precondição (vem do header <c>If-Match</c>), mesmo
/// padrão de <see cref="DefinirClassificacaoRequest"/>.
/// </summary>
public sealed record DefinirFormularioRequest(string? Titulo, string? TermoAceiteTexto);
