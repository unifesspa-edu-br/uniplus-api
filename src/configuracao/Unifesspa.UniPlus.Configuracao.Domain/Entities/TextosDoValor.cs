namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>A descrição e a orientação de um valor do fato, já aparadas; <see langword="null"/> quando em branco.</summary>
internal readonly record struct TextosDoValor(string? Descricao, string? Orientacao);

/// <summary>O valor que o fato vai receber, depois de conferido: o código aparado, os textos e a ordem.</summary>
internal readonly record struct NovoValorDoFato(string Codigo, TextosDoValor Textos, int Ordem);
