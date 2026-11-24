namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Vista ativa dos tipos de conceito para consumo cross-módulo (ADR-0056).</summary>
/// <remarks>
/// <paramref name="Natureza"/> é o código declarado de <c>FormaDoConceito</c> (ADR-0176).
/// <paramref name="Numerico"/> só vem no tipo numérico, e <paramref name="Valores"/> só no categórico, na
/// ordem do cadastro. Quem consome congela a própria cópia por valor.
/// </remarks>
public sealed record TipoConceitoView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Natureza,
    EscalaNumericaView? Numerico,
    IReadOnlyList<ValorCategoricoView> Valores);

/// <summary>A escala de um tipo de conceito numérico.</summary>
public sealed record EscalaNumericaView(decimal Minima, decimal Maxima, int CasasDecimais);

/// <summary>Um valor de um tipo de conceito categórico.</summary>
public sealed record ValorCategoricoView(string Codigo, string Rotulo, bool ContaComoAprovacao);
