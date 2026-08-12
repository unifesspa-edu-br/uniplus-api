namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirTaxaInscricao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Cobra</c> é <c>[JsonRequired]</c> (mesmo raciocínio de
/// <c>DefinirClassificacaoRequest.BaseadoEmEnem</c>, issue #850): o host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então por padrão do <c>System.Text.Json</c> um
/// parâmetro de construtor ausente no JSON recebe o valor default (<see langword="null"/>) —
/// indistinguível de <c>cobra: null</c> explícito, que o handler interpreta como remoção
/// deliberada da declaração (CA-01). Omitir o campo não pode desfazer silenciosamente uma
/// configuração existente.
/// </remarks>
public sealed record DefinirTaxaInscricaoRequest(
    [property: JsonRequired] bool? Cobra,
    decimal? Valor,
    IReadOnlyList<string>? Fundamentos,
    bool ConfirmacaoFundamentos);
