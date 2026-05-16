namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

/// <summary>
/// Helper para mapear violações 23505 de índice UNIQUE parcial para
/// <c>DomainError</c> apropriado. Acessa <c>SqlState</c> e
/// <c>ConstraintName</c> via reflection no inner exception — o shape é
/// estável na API pública do <c>Npgsql.PostgresException</c>. Inspeção
/// do tipo da exceção por nome via <see cref="System.Type.FullName"/>
/// evita dependência direta do <c>Microsoft.EntityFrameworkCore</c>
/// package na camada Application (mantém Clean Arch — Application
/// referencia apenas Domain + SharedKernel + abstrações). Mapping
/// centralizado para 23505 cross-cutting permanece como #504 (escopo
/// separado); este helper cobre o caso específico das constraints que
/// esta Story introduz.
/// </summary>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string RegraCodigoConstraint = "ux_obrigatoriedades_legais_regra_codigo_ativos";

    private const string HashConstraint = "ux_obrigatoriedades_legais_hash_ativos";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> wrapping uma <c>PostgresException</c> com
    /// <c>SqlState = "23505"</c>. <see langword="null"/> caso contrário —
    /// o caller deve propagar a exceção.
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
    /// <see langword="true"/> quando a constraint violada é a do
    /// <c>RegraCodigo</c> ativo.
    /// </summary>
    public static bool IsRegraCodigoConflict(string? constraint) =>
        string.Equals(constraint, RegraCodigoConstraint, StringComparison.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é a do
    /// <c>Hash</c> canônico de regra ativa.
    /// </summary>
    public static bool IsHashConflict(string? constraint) =>
        string.Equals(constraint, HashConstraint, StringComparison.Ordinal);
}
