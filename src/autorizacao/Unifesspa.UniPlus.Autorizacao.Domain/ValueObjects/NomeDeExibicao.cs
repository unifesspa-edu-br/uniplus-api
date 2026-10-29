namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Regra única do nome de exibição de quem age: informa só quem lê a trilha e o aprovador, e não identifica a pessoa.
/// </summary>
public static class NomeDeExibicao
{
    /// <summary>Tamanho máximo do nome, também o das colunas que o gravam.</summary>
    public const int MaxLength = 255;

    /// <summary>
    /// O nome aparado; nulo quando vazio ou com caractere nulo, que a coluna textual do banco não armazena. Vindo longo
    /// demais, perde o excedente em vez de impedir a operação.
    /// </summary>
    public static string? Normalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        string aparado = nome.Trim();

        return aparado.Length > MaxLength ? aparado[..MaxLength] : aparado;
    }
}
