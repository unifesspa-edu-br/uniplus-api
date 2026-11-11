namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

/// <summary>
/// Traduz a violação <c>23505</c> do índice único do código do motivo dentro do tipo. Mesmo
/// desenho do helper irmão das obrigatoriedades legais: o tipo da exceção é
/// inspecionado pelo nome para que a camada Application não passe a depender do
/// pacote do EF Core.
/// </summary>
internal static class MotivoDeDecisaoUniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = "ux_motivos_de_decisao_tipo_codigo";

    private const string EscolhaConstraint = "ux_motivos_do_processo_processo_origem";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// <see langword="true"/> quando a exceção é a violação do índice único do
    /// código. Qualquer outra coisa devolve <see langword="false"/>, e quem
    /// chama propaga — traduzir o que não se reconhece esconderia um defeito
    /// atrás de um <c>409</c> plausível.
    /// </summary>
    public static bool EhConflitoDeCodigo(Exception ex) => EhViolacaoDe(ex, CodigoConstraint);

    /// <summary>A mesma tradução para a escolha repetida do mesmo motivo pelo processo, em corrida.</summary>
    public static bool EhEscolhaRepetida(Exception ex) => EhViolacaoDe(ex, EscolhaConstraint);

    private static bool EhViolacaoDe(Exception ex, string constraint)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            || ex.InnerException is not { } inner)
        {
            return false;
        }

        Type innerType = inner.GetType();
        return innerType.GetProperty("SqlState")?.GetValue(inner) as string == UniqueViolationSqlState
            && string.Equals(innerType.GetProperty("ConstraintName")?.GetValue(inner) as string, constraint, StringComparison.Ordinal);
    }
}
