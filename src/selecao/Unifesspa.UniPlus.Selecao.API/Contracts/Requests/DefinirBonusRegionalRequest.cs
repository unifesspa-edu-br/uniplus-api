namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirBonusRegional"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Aplica</c> é <c>[JsonRequired]</c>: o host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então um <c>bool</c> ausente no JSON receberia
/// <see langword="false"/> — indistinguível de um "não aplica" explícito. Omitir a declaração não
/// pode equivaler silenciosamente a declarar que o processo não aplica o bônus.
/// </remarks>
public sealed record DefinirBonusRegionalRequest(
    [property: JsonRequired] bool Aplica,
    string? RegraCodigo,
    string? RegraVersao,
    decimal? Fator,
    decimal? Teto,
    Guid? BaseLegalBonusRegionalId);
