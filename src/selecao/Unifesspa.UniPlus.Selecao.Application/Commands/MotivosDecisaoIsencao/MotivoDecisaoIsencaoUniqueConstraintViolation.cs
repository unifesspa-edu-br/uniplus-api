namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

/// <summary>
/// Traduz a violação <c>23505</c> do índice único do código do motivo. Mesmo
/// desenho do helper irmão das obrigatoriedades legais: o tipo da exceção é
/// inspecionado pelo nome para que a camada Application não passe a depender do
/// pacote do EF Core.
/// </summary>
internal static class MotivoDecisaoIsencaoUniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = "ux_motivos_decisao_isencao_codigo";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// <see langword="true"/> quando a exceção é a violação do índice único do
    /// código. Qualquer outra coisa devolve <see langword="false"/>, e quem
    /// chama propaga — traduzir o que não se reconhece esconderia um defeito
    /// atrás de um <c>409</c> plausível.
    /// </summary>
    public static bool EhConflitoDeCodigo(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal))
        {
            return false;
        }

        if (ex.InnerException is not { } inner)
        {
            return false;
        }

        Type innerType = inner.GetType();

        if (innerType.GetProperty("SqlState")?.GetValue(inner) as string != UniqueViolationSqlState)
        {
            return false;
        }

        return string.Equals(
            innerType.GetProperty("ConstraintName")?.GetValue(inner) as string,
            CodigoConstraint,
            StringComparison.Ordinal);
    }
}
