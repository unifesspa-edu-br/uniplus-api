namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using System.Text.Json.Serialization;

/// <summary>
/// Um valor do tipo de conceito categórico como chega no corpo da requisição. Se conta como aprovação é
/// obrigatório: um bool ausente viraria <c>false</c> em silêncio, e o valor nasceria sem aprovar.
/// </summary>
public sealed record ValorCategoricoCommand(
    string? Codigo,
    string? Rotulo,
    [property: JsonRequired] bool ContaComoAprovacao);
