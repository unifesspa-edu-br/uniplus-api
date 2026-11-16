namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A regra única do texto livre que o usuário digita e o módulo grava.</summary>
public static class TextoLivre
{
    /// <summary>
    /// O texto aparado, ou nulo quando está em branco, fora dos limites de tamanho ou traz o caractere nulo, que não é espaço
    /// em branco e cabe no tamanho, mas a coluna textual do banco não armazena: sem esta recusa o valor só falharia ao gravar,
    /// e o payload inválido responderia 500 em vez da validação de domínio.
    /// </summary>
    public static string? Normalizar(string? texto, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string normalizado = texto.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length < min || normalizado.Length > max
            ? null
            : normalizado;
    }
}
