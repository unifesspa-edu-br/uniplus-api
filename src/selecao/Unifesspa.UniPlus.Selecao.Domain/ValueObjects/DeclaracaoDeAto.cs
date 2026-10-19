namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Um tipo de ato que o cronograma do processo declara publicar, e o lugar que o declara.</summary>
/// <param name="AtoCodigo">Código do tipo de ato.</param>
/// <param name="Lugar">Onde a declaração está, já em texto para o operador ("na fase 'X'").</param>
/// <param name="Papel">O papel declarado para o produto, ou nulo quando ele não tem papel.</param>
public sealed record DeclaracaoDeAto(string AtoCodigo, string Lugar, PapelProdutoFase? Papel = null);
