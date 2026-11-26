namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

public sealed record DefinirCadastroDeReservaRequest(
    [property: JsonRequired] IReadOnlyList<CadastroDeReservaInformado> Chamadas);
