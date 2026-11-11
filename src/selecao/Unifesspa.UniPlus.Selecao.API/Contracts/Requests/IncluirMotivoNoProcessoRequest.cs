namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoMotivosController.Incluir"/>. A justificativa é exigida só
/// depois da publicação, na retificação, e é de uso interno: não é apresentada ao candidato.
/// </summary>
public sealed record IncluirMotivoNoProcessoRequest(string? Justificativa);
