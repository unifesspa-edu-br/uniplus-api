namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoOfertaEClassificacaoController.DefinirTaxaInscricao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Cobra</c> é <see cref="bool"/> anulável, sem valor padrão e sem <c>[JsonRequired]</c>: ausente ou nulo chega
/// ao domínio e é recusado (422), acumulado com as demais recusas. Antes, o nulo removia a declaração; omitir o
/// campo não pode desfazer em silêncio uma configuração existente, e remover a declaração deixou de ser possível
/// por este corpo. O contrato publica o campo como obrigatório e sem <c>null</c>.
/// </remarks>
public sealed record DefinirTaxaInscricaoRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422). Nulo não remove mais a declaração.")]
    bool? Cobra,
    decimal? Valor,
    [property: AceitaCodigosDe<FundamentoIsencao>]
    IReadOnlyList<string>? Fundamentos);
