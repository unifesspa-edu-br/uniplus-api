namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// Corpo da definição da prova presencial. Os dois campos são anuláveis, sem valor padrão e sem
/// <c>[JsonRequired]</c>: ausentes ou nulos chegam ao domínio e são recusados (422), acumulados com as demais
/// recusas, em vez de virarem "sem prova presencial" ou o 400 da leitura do corpo.
/// </summary>
public sealed record DefinirProvaPresencialRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? TemProvaPresencial,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<MunicipioDeProvaInformado>? Municipios);
