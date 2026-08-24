namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

/// <summary>
/// Helper para mapear violações 23505 dos índices únicos parciais do tipo de
/// deficiência — <c>ix_tipo_deficiencia_codigo_vivo</c> e
/// <c>ix_tipo_deficiencia_nome_vivo</c> — para o <c>DomainError</c> apropriado.
/// Acessa <c>SqlState</c> e <c>ConstraintName</c> por reflection no inner
/// exception — o shape é estável na API pública do <c>Npgsql.PostgresException</c>.
/// A inspeção do tipo da exceção por <see cref="System.Type.FullName"/> evita
/// dependência direta do pacote <c>Microsoft.EntityFrameworkCore</c> na camada
/// Application (mantém Clean Arch — Application referencia apenas Domain +
/// SharedKernel + abstrações).
/// </summary>
/// <remarks>
/// Mesmo padrão do <c>UniqueConstraintViolation</c> do cadastro de Tipo de
/// documento (#591). A tradução cross-cutting de 23505 → 409 via
/// <c>GlobalExceptionMiddleware</c> permanece como follow-up separado (#504); este
/// helper cobre os casos específicos das duas unicidades, inclusive a corrida na
/// atualização (código e nome são editáveis). Como a tabela tem <b>dois</b>
/// índices únicos parciais, o caller precisa discriminar qual foi violado — um
/// <c>CodigoJaExiste</c> devolvido para uma colisão de nome mentiria sobre a causa.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = "ix_tipo_deficiencia_codigo_vivo";

    private const string NomeConstraint = "ix_tipo_deficiencia_nome_vivo";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> wrapping uma <c>PostgresException</c> com
    /// <c>SqlState = "23505"</c>. <see langword="null"/> caso contrário — o
    /// caller deve propagar a exceção.
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
    /// que garante um único tipo vivo por código.
    /// </summary>
    public static bool IsCodigoConflict(string? constraint) =>
        string.Equals(constraint, CodigoConstraint, StringComparison.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é o índice único parcial
    /// que garante um único tipo vivo por nome.
    /// </summary>
    public static bool IsNomeConflict(string? constraint) =>
        string.Equals(constraint, NomeConstraint, StringComparison.Ordinal);
}
