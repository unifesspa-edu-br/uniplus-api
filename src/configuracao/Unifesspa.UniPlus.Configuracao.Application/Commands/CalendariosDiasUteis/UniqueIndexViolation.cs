namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

/// <summary>
/// Detecta a violação 23505 (unique_violation) do índice
/// <c>ix_dia_nao_util_unicidade</c> (api#1458) por <see cref="Type.FullName"/> — evita
/// dependência direta de <c>Npgsql</c>/<c>Microsoft.EntityFrameworkCore</c> na camada
/// Application (mesmo raciocínio de <see cref="ExclusionConstraintViolation"/> e
/// <see cref="OptimisticConcurrencyViolation"/>).
/// </summary>
/// <remarks>
/// Diferente da exclusion constraint de vigência (<c>DEFERRABLE INITIALLY
/// DEFERRED</c>, checada só no commit), este é um índice único comum: a violação
/// chega embrulhada em <c>DbUpdateException</c> durante a execução do
/// <c>INSERT</c>, nunca como <c>Npgsql.PostgresException</c> bruta.
/// </remarks>
internal static class UniqueIndexViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string DiaNaoUtilUnicidadeIndex = "ix_dia_nao_util_unicidade";

    private const string PostgresExceptionFullName = "Npgsql.PostgresException";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary><see langword="true"/> quando <paramref name="ex"/> é a violação do índice de unicidade de dia não útil.</summary>
    public static bool IsDiaNaoUtilDuplicado(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return string.Equals(GetViolatedConstraint(ex), DiaNaoUtilUnicidadeIndex, StringComparison.Ordinal);
    }

    private static string? GetViolatedConstraint(Exception ex)
    {
        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            || ex.InnerException is null)
        {
            return null;
        }

        Exception pgCandidate = ex.InnerException;
        Type pgType = pgCandidate.GetType();
        if (!string.Equals(pgType.FullName, PostgresExceptionFullName, StringComparison.Ordinal))
        {
            return null;
        }

        string? sqlState = pgType.GetProperty("SqlState")?.GetValue(pgCandidate) as string;
        if (sqlState != UniqueViolationSqlState)
        {
            return null;
        }

        return pgType.GetProperty("ConstraintName")?.GetValue(pgCandidate) as string;
    }
}
