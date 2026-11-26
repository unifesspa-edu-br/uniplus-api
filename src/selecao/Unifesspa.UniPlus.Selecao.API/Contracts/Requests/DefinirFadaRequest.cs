namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Application.Commands.ProcessosSeletivos;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoOfertaEClassificacaoController.DefinirFada"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Aplica</c> é <c>[JsonRequired]</c>: o host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então um <c>bool</c> ausente no JSON receberia
/// <see langword="false"/> — indistinguível de um "não aplica" explícito. Omitir a declaração não
/// pode equivaler silenciosamente a declarar que o processo não aplica o FADA.
/// </remarks>
public sealed record DefinirFadaRequest(
    [property: JsonRequired] bool Aplica,
    ReferenciaRegraInput? Regra,
    decimal? Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalDoFadaInput? BaseLegal);
