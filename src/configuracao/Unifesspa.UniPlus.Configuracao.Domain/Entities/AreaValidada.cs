namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Uma área já conferida, com o peso e o corte arredondados à escala gravada.</summary>
internal sealed record AreaValidada(AreaEnemDescrita Descrita, decimal Peso, decimal? Corte);

/// <summary>Os campos editáveis de uma linha de Pesos por Área depois de conferidos.</summary>
internal sealed record CamposEditaveis(IReadOnlyList<AreaValidada> Areas, string BaseLegal);
