namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirTaxaInscricao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirTaxaInscricaoRequest(
    bool? Cobra,
    decimal? Valor,
    IReadOnlyList<string>? Fundamentos,
    bool ConfirmacaoFundamentos);
