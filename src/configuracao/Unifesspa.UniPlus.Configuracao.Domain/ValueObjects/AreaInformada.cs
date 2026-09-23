namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Peso e corte de uma área como chegam na gravação de Pesos por Área, ainda não
/// validados: o <see cref="Codigo"/> identifica a área e pode estar fora das cinco; o
/// agregado confere tudo e acumula as recusas.
/// </summary>
/// <param name="Codigo">Código da área informado, ou <see langword="null"/> quando ausente.</param>
/// <param name="Peso">Peso informado para a área.</param>
/// <param name="Corte">Nota mínima informada, ou <see langword="null"/> para sem corte.</param>
public sealed record AreaInformada(
    string? Codigo,
    decimal Peso,
    decimal? Corte);
