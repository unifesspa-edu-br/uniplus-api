namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>A escala de um tipo de conceito numérico como chega na gravação, ainda não validada.</summary>
/// <param name="EscalaMinima">Menor valor da escala, ou <see langword="null"/> quando ausente.</param>
/// <param name="EscalaMaxima">Maior valor da escala, ou <see langword="null"/> quando ausente.</param>
/// <param name="CasasDecimais">Casas decimais da escala, ou <see langword="null"/> quando ausente.</param>
public sealed record EscalaInformada(decimal? EscalaMinima, decimal? EscalaMaxima, int? CasasDecimais);
