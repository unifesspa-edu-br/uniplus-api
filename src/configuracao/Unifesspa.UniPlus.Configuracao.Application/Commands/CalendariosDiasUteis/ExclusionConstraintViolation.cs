namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

/// <summary>
/// Helper para mapear violações 23P01 (exclusion_violation) da exclusion constraint
/// GiST <c>ex_calendario_dias_uteis_vigente_unico</c> (issue #1016) para o
/// <c>DomainError</c> de conflito. A inspeção do tipo da exceção por
/// <see cref="Type.FullName"/> evita dependência direta de <c>Npgsql</c>/
/// <c>Microsoft.EntityFrameworkCore</c> na camada Application (mantém Clean Arch).
/// </summary>
/// <remarks>
/// Diferente do <c>UniqueConstraintViolation</c> dos demais cadastros (que só
/// olham dentro de um <c>DbUpdateException</c>): como a constraint é
/// <c>DEFERRABLE INITIALLY DEFERRED</c>, a checagem só roda no <c>COMMIT</c> da
/// transação — a violação chega como <c>Npgsql.PostgresException</c> BRUTA de
/// <c>NpgsqlTransaction.Commit</c>, não embrulhada em <c>DbUpdateException</c> (que
/// o EF Core só produz para falhas durante a execução do batch de comandos). Este
/// helper cobre os dois formatos.
/// </remarks>
internal static class ExclusionConstraintViolation
{
    private const string ExclusionViolationSqlState = "23P01";

    private const string VigenteConstraint = "ex_calendario_dias_uteis_vigente_unico";

    private const string PostgresExceptionFullName = "Npgsql.PostgresException";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary><see langword="true"/> quando <paramref name="ex"/> é a violação da exclusion constraint de vigência.</summary>
    public static bool IsVigenteConflict(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return string.Equals(GetViolatedConstraint(ex), VigenteConstraint, StringComparison.Ordinal);
    }

    private static string? GetViolatedConstraint(Exception ex)
    {
        Exception pgCandidate = ex;

        if (string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal))
        {
            if (ex.InnerException is null)
            {
                return null;
            }

            pgCandidate = ex.InnerException;
        }
        else if (!string.Equals(ex.GetType().FullName, PostgresExceptionFullName, StringComparison.Ordinal))
        {
            return null;
        }

        Type pgType = pgCandidate.GetType();
        string? sqlState = pgType.GetProperty("SqlState")?.GetValue(pgCandidate) as string;
        if (sqlState != ExclusionViolationSqlState)
        {
            return null;
        }

        return pgType.GetProperty("ConstraintName")?.GetValue(pgCandidate) as string;
    }
}
