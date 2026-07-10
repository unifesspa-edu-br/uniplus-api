namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>
/// Mapeia a violação <c>23P01</c> da exclusion constraint
/// <c>ex_tipo_ato_publicado_codigo_vigencia</c> para o <c>DomainError</c>
/// apropriado. Acessa <c>SqlState</c> e <c>ConstraintName</c> por reflection no
/// inner exception — o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> nesta camada (Clean Architecture —
/// Application referencia apenas Domain, Kernel e abstrações).
/// </summary>
/// <remarks>
/// <para>Mesmo padrão dos helpers <c>UniqueConstraintViolation</c> dos cadastros de
/// Configuração e Seleção, com o SQLSTATE de exclusão em vez do de unicidade.</para>
/// <para>O PostgreSQL preenche o nome da constraint em toda a classe 23 de erros, e
/// os caminhos que lançam <c>EXCLUSION_VIOLATION</c> o incluem. Quando o nome vier
/// ausente, a exceção <b>propaga</b>: converter uma exclusion constraint desconhecida
/// num conflito de vigência seria mentir sobre a causa.</para>
/// </remarks>
internal static class ExclusionConstraintViolation
{
    private const string ExclusionViolationSqlState = "23P01";

    private const string VigenciaConstraint = "ex_tipo_ato_publicado_codigo_vigencia";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> envolvendo uma <c>PostgresException</c> com
    /// <c>SqlState = "23P01"</c>. <see langword="null"/> caso contrário — o caller
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
        if (sqlState != ExclusionViolationSqlState)
        {
            return null;
        }

        return innerType.GetProperty("ConstraintName")?.GetValue(inner) as string;
    }

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é a que impede duas versões
    /// vivas do mesmo código de valerem no mesmo dia.
    /// </summary>
    public static bool IsVigenciaConflict(string? constraint) =>
        string.Equals(constraint, VigenciaConstraint, StringComparison.Ordinal);
}
