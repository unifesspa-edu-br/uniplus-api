namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

/// <summary>
/// Reconhece, pelo nome do tipo, as exceções de gravação que os handlers traduzem em erro de
/// domínio, para que a camada Application não dependa do pacote do EF Core.
/// </summary>
internal static class ViolacoesDeGravacao
{
    private const string UniqueViolationSqlState = "23505";
    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";
    private const string DbUpdateConcurrencyExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException";

    /// <summary>
    /// Violação de um dos índices únicos informados. Qualquer outra exceção devolve falso e quem
    /// chama propaga: traduzir o que não se reconhece esconderia um defeito atrás de um <c>409</c>.
    /// </summary>
    public static bool ViolaIndiceUnico(Exception ex, params string[] indices)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            || ex.InnerException is not { } inner)
        {
            return false;
        }

        Type tipo = inner.GetType();
        string? constraint = tipo.GetProperty("ConstraintName")?.GetValue(inner) as string;

        return tipo.GetProperty("SqlState")?.GetValue(inner) as string == UniqueViolationSqlState
            && indices.Contains(constraint, StringComparer.Ordinal);
    }

    /// <summary>
    /// A gravação amarrada ao <c>xmin</c> lido não encontrou a linha na versão esperada: outra
    /// requisição alterou a entidade entre a leitura e a gravação.
    /// </summary>
    public static bool EhConflitoDeConcorrencia(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return string.Equals(ex.GetType().FullName, DbUpdateConcurrencyExceptionFullName, StringComparison.Ordinal);
    }
}
