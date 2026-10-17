namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Um tipo de ato que o cronograma do processo declara publicar, e o lugar que o declara.</summary>
/// <param name="AtoCodigo">Código do tipo de ato.</param>
/// <param name="Lugar">Onde a declaração está, já em texto para o operador ("na fase 'X'").</param>
public sealed record DeclaracaoDeAto(string AtoCodigo, string Lugar);
