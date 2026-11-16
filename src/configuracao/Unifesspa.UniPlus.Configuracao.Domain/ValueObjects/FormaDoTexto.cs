namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>O que um campo textual aceita além das letras: define o que a normalização recusa.</summary>
internal enum FormaDoTexto
{
    /// <summary>Uma linha de texto: recusa o nulo, os controles — inclusive a quebra de linha e a tabulação — e a formatação.</summary>
    UmaLinha = 0,

    /// <summary>Várias linhas de texto: aceita a quebra de linha e a tabulação e recusa o nulo, os demais controles e a formatação.</summary>
    VariasLinhas = 1,

    /// <summary>Texto livre colado de outro documento: recusa só o caractere nulo.</summary>
    Livre = 2,
}
