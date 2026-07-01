namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

/// <summary>
/// Helper para mapear violações 23505 do índice único parcial do código vivo da
/// fase — <c>ix_fase_canonica_codigo_vivo</c> — para o <c>DomainError</c>
/// apropriado. Acessa <c>SqlState</c> e <c>ConstraintName</c> por reflection no
/// inner exception — o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> na camada Application (mantém Clean Arch).
/// </summary>
/// <remarks>
/// Mesmo padrão do <c>UniqueConstraintViolation</c> dos demais cadastros de
/// Configuração. O código é imutável, então só há corrida na criação.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = "ix_fase_canonica_codigo_vivo";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> wrapping uma <c>PostgresException</c> com
    /// <c>SqlState = "23505"</c>. <see langword="null"/> caso contrário — o caller
    /// deve propagar a exceção.
    /// </summary>
    public static string? GetViolatedConstraint(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal))
        {
            return null;
        }

        Exception? inner = ex.InnerException;
        if (inner is null)
        {
            return null;
        }

        Type innerType = inner.GetType();
        string? sqlState = innerType.GetProperty("SqlState")?.GetValue(inner) as string;
        if (sqlState != UniqueViolationSqlState)
        {
            return null;
        }

        return innerType.GetProperty("ConstraintName")?.GetValue(inner) as string;
    }

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é o índice único parcial
    /// que garante uma única fase viva por código.
    /// </summary>
    public static bool IsCodigoConflict(string? constraint) =>
        string.Equals(constraint, CodigoConstraint, StringComparison.Ordinal);
}
