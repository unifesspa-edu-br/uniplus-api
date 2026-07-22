namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirBonusRegional"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirBonusRegionalRequest(
    string? RegraCodigo,
    string? RegraVersao,
    decimal? Fator,
    decimal? Teto,
    string? MunicipioConvenio,
    string? BaseLegal);
