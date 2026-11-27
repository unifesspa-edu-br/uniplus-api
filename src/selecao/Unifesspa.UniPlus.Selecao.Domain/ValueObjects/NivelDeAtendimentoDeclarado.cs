namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um nível da escala de atendimento como o pedido o declara, antes de qualquer conferência: o rótulo, a fração e a chave
/// podem faltar, e é a conferência da escala que recusa a falta. O <see cref="NivelDeAtendimento"/> só existe depois dela.
/// </summary>
public sealed record NivelDeAtendimentoDeclarado(string? Rotulo, decimal? Fracao, string? Chave = null);
