namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>A escala de um conceito numérico como o catálogo a informa, ainda por conferir.</summary>
public sealed record EscalaInformada(decimal Minima, decimal Maxima, int CasasDecimais);

/// <summary>Um valor de um conceito categórico como o catálogo o informa, ainda por conferir.</summary>
public sealed record ValorCategoricoInformado(string? Codigo, string? Rotulo, bool ContaComoAprovacao);
