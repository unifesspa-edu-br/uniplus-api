namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Application.Commands.ProcessosSeletivos;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoOfertaEClassificacaoController.DefinirFada"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Aplica</c> é <see cref="bool"/> anulável, sem valor padrão e sem <c>[JsonRequired]</c>: ausente ou nulo chega ao
/// domínio e é recusado (422), em vez de virar <see langword="false"/> — um "não aplica" que o cliente não declarou.
/// O contrato publica o campo como obrigatório e sem <c>null</c>.
/// </remarks>
public sealed record DefinirFadaRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? Aplica,
    ReferenciaRegraInput? Regra,
    decimal? Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalDoFadaInput? BaseLegal);
