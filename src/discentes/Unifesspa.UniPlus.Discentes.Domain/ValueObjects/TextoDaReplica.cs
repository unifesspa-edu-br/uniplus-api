namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As duas recusas que os textos da réplica têm em comum. A réplica guarda o texto como o
/// SIGAA mandou: o tamanho é medido sobre o texto inalterado, sem aparar espaços.
/// </summary>
internal static class TextoDaReplica
{
    /// <summary>Recusa o texto ausente ou em branco.</summary>
    public static DomainError? RecusarSeVazio(string? texto, string codigo, string mensagem) =>
        string.IsNullOrWhiteSpace(texto) ? new DomainError(codigo, mensagem) : null;

    /// <summary>Recusa o texto que passa do que a coluna da réplica comporta; o ausente cabe.</summary>
    public static DomainError? RecusarSeExceder(string? texto, int limite, string codigo, string mensagem) =>
        texto is not null && texto.Length > limite ? new DomainError(codigo, mensagem) : null;
}
