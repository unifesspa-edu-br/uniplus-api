namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

/// <summary>A escala do tipo de conceito numérico como chega no corpo da requisição.</summary>
public sealed record EscalaNumericaCommand(decimal? EscalaMinima, decimal? EscalaMaxima, int? CasasDecimais);
