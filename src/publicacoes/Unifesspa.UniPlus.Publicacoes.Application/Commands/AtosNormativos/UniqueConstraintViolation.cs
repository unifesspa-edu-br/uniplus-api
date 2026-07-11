namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

/// <summary>
/// Mapeia a violação <c>23505</c> (unique_violation) do índice único parcial
/// <c>ux_ato_normativo_retificado</c> para o <c>DomainError</c> apropriado — a
/// garantia dura da linearidade da cadeia de retificação (ADR-0103) contra a
/// corrida check-then-act do handler. Acessa <c>SqlState</c> e
/// <c>ConstraintName</c> por reflection no inner exception; o shape é estável na
/// API pública do <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção
/// por <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> nesta camada (Clean Architecture —
/// Application referencia apenas Domain, Kernel e abstrações).
/// </summary>
/// <remarks>
/// Mesmo padrão de <c>ExclusionConstraintViolation</c> do cadastro de tipos de
/// ato, com o SQLSTATE de unicidade em vez do de exclusão. A consulta prévia do
/// handler (<c>ObterRetificadorAsync</c>) dá a mensagem que nomeia o retificador
/// no caso comum; o índice único fecha a corrida — entre a consulta e o
/// <c>SaveChanges</c> cabe outra transação, e só o banco a vê.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string LinearidadeConstraint = "ux_ato_normativo_retificado";

    private const string LinhagemUnicaConstraint = "ux_linhagem_unica_por_objeto";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> envolvendo uma <c>PostgresException</c> com
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
    /// <see langword="true"/> quando a constraint violada é a que impede um mesmo
    /// ato de ser retificado duas vezes (cadeia linear, ADR-0103).
    /// </summary>
    public static bool IsLinearidadeConflict(string? constraint) =>
        string.Equals(constraint, LinearidadeConstraint, StringComparison.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é a vaga do objeto: outra
    /// linhagem reservou-a entre a consulta do handler e o <c>SaveChanges</c>
    /// (ADR-0107). É o que fecha a corrida check-then-act da unicidade por objeto.
    /// </summary>
    public static bool IsLinhagemUnicaConflict(string? constraint) =>
        string.Equals(constraint, LinhagemUnicaConstraint, StringComparison.Ordinal);
}
