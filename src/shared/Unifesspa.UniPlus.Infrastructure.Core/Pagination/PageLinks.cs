namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// URLs de navegação RFC 5988/8288 de uma página: <c>self</c> sempre presente;
/// <c>next</c>/<c>prev</c> ausentes quando o cursor correspondente não existe
/// (extremos da janela).
/// </summary>
public sealed record PageLinks(string Self, string? Next, string? Prev);
