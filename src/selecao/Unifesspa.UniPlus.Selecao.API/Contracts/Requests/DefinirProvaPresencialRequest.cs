namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

public sealed record DefinirProvaPresencialRequest(
    [property: JsonRequired] bool TemProvaPresencial,
    [property: JsonRequired] IReadOnlyList<MunicipioDeProvaInformado> Municipios);
