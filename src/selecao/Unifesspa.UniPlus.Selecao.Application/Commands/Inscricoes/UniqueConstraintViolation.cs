namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

/// <summary>
/// Extrai o NOME da constraint de banco violada por uma gravação da inscrição, para que quem
/// chama decida o que fazer — mesmo padrão de <c>ProcessosSeletivos/UniqueConstraintViolation.cs</c>:
/// inspeção do tipo da exceção por <see cref="System.Type.FullName"/> evita dependência direta do
/// pacote <c>Microsoft.EntityFrameworkCore</c> na camada Application.
/// </summary>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma <c>DbUpdateException</c>
    /// wrapping uma <c>PostgresException</c> com <c>SqlState = "23505"</c>. <see langword="null"/>
    /// caso contrário — o caller deve propagar a exceção.
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
        if (innerType.GetProperty("SqlState")?.GetValue(inner) as string != UniqueViolationSqlState)
        {
            return null;
        }

        return innerType.GetProperty("ConstraintName")?.GetValue(inner) as string;
    }
}
