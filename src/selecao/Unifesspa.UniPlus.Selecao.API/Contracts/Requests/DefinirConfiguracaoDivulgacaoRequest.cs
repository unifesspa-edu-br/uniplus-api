namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirConfiguracaoDivulgacao"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota). Não aceita a regra de abreviação: ela nunca é
/// escolha do cliente, é derivada da regra vigente no instante do congelamento.
/// </summary>
public sealed record DefinirConfiguracaoDivulgacaoRequest(
    IReadOnlyList<string>? CamposPublicos,
    string? Justificativa);
