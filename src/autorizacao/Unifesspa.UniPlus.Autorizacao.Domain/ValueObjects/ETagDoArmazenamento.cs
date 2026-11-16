namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O ETag que o armazenamento devolve ao receber um arquivo, como o módulo o guarda para o verificador conferir.</summary>
public static class ETagDoArmazenamento
{
    /// <summary>O tamanho máximo do ETag nas colunas do módulo.</summary>
    public const int MaxLength = 200;

    /// <summary>O ETag sem espaços nas pontas; nulo quando falta, passa de <see cref="MaxLength"/> ou traz o caractere nulo, que a coluna textual não armazena.</summary>
    public static string? Normalizar(string? etag) =>
        string.IsNullOrWhiteSpace(etag) || etag.Length > MaxLength || etag.Contains('\0', StringComparison.Ordinal)
            ? null
            : etag.Trim();
}
