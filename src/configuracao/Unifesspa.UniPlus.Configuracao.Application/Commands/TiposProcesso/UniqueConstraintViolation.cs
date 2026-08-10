namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

/// <summary>Traduz a corrida do índice único sem acoplar Application ao provider EF/Npgsql.</summary>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";
    private const string Constraint = "ix_tipos_processo_codigo";

    public static bool EhConflitoDeCodigo(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!string.Equals(exception.GetType().FullName, "Microsoft.EntityFrameworkCore.DbUpdateException", StringComparison.Ordinal)
            || exception.InnerException is null)
        {
            return false;
        }

        Type tipo = exception.InnerException.GetType();
        return string.Equals(tipo.GetProperty("SqlState")?.GetValue(exception.InnerException) as string, UniqueViolationSqlState, StringComparison.Ordinal)
            && string.Equals(tipo.GetProperty("ConstraintName")?.GetValue(exception.InnerException) as string, Constraint, StringComparison.Ordinal);
    }
}
