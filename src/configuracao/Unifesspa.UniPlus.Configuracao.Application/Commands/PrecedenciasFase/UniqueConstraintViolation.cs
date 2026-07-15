namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

/// <summary>
/// Helper para mapear violações 23505 do índice único parcial do par vivo
/// (antecessora, sucessora) — <c>ix_precedencia_fase_par_vivo</c> — para o
/// <c>DomainError</c> apropriado. Acessa <c>SqlState</c> e <c>ConstraintName</c>
/// por reflection no inner exception — o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> na camada Application (mantém Clean Arch).
/// </summary>
/// <remarks>
/// Mesmo padrão do <c>UniqueConstraintViolation</c> dos demais cadastros de
/// Configuração. Protege a corrida check-then-act entre o carregamento do grafo
/// vigente (<c>ListarVivasAsync</c>) e o INSERT — a mesma classe de corrida do
/// código de <c>FaseCanonica</c>.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string ParConstraint = "ix_precedencia_fase_par_vivo";

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
    /// que garante um único par (antecessora, sucessora) vivo.
    /// </summary>
    public static bool IsParConflict(string? constraint) =>
        string.Equals(constraint, ParConstraint, StringComparison.Ordinal);
}
