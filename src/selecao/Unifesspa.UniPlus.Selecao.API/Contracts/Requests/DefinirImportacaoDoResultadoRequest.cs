namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// Corpo da declaração da importação do resultado. A lista de relatórios é anulável e sem <c>[JsonRequired]</c>: ausente ou nula chega
/// ao domínio e é recusada (422), junto das demais recusas; a lista vazia declara "nenhum relatório".
/// </summary>
public sealed record DefinirImportacaoDoResultadoRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<RelatorioInformado>? Relatorios,
    Guid? PrimeiraFaseDepoisDaImportacaoId);
