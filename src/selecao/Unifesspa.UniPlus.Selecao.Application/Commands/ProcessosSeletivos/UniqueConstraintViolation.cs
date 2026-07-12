namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Extrai o NOME da constraint de banco violada (índice UNIQUE ou CHECK) do
/// ciclo de publicação do Processo Seletivo, para que
/// <see cref="VersaoConfiguracaoConstraintViolation"/> a traduza em
/// <c>DomainError</c> nomeado (RN08, ADR-0102). Mesmo padrão de
/// <c>ObrigatoriedadesLegais/UniqueConstraintViolation.cs</c>: inspeção do tipo
/// da exceção por nome via <see cref="System.Type.FullName"/> evita dependência
/// direta do package <c>Microsoft.EntityFrameworkCore</c> na camada Application
/// (mantém Clean Arch).
/// </summary>
/// <remarks>
/// A tradução acontece no boundary de persistência (ADR-0102 §"Forma do
/// contrato") no sentido em que é aplicada imediatamente em torno da chamada
/// a <c>ISelecaoUnitOfWork.SalvarAlteracoesAsync</c> — o ponto exato em que
/// Application invoca o boundary de persistência — não dentro do
/// repositório. Mesma convenção já usada por 7+ handlers do módulo antes
/// desta ADR existir; não introduz uma segunda forma de tradução.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";
    private const string CheckViolationSqlState = "23514";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// Retorna o nome da constraint violada quando a exceção é uma
    /// <c>DbUpdateException</c> wrapping uma <c>PostgresException</c> com
    /// <c>SqlState</c> de violação de UNIQUE (23505) ou CHECK (23514).
    /// <see langword="null"/> caso contrário — o caller deve propagar a exceção.
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
        if (sqlState != UniqueViolationSqlState && sqlState != CheckViolationSqlState)
        {
            return null;
        }

        return innerType.GetProperty("ConstraintName")?.GetValue(inner) as string;
    }
}
