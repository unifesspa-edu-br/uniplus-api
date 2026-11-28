namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// Corpo da definição do cadastro de reserva. A lista é anulável e sem <c>[JsonRequired]</c>: ausente ou nula chega ao
/// domínio e é recusada (422); a lista vazia declara "nenhuma chamada com reserva".
/// </summary>
public sealed record DefinirCadastroDeReservaRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<CadastroDeReservaInformado>? Chamadas);
