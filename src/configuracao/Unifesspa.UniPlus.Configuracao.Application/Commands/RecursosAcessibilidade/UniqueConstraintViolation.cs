namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Helper para mapear violações 23505 dos índices únicos parciais do recurso de
/// acessibilidade — <c>ix_recurso_acessibilidade_codigo_vivo</c> e
/// <c>ix_recurso_acessibilidade_nome_vivo</c> — para o <c>DomainError</c> da
/// constraint efetivamente violada. Acessa <c>SqlState</c> e <c>ConstraintName</c>
/// por reflection no inner exception — o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> na camada Application (mantém Clean Arch —
/// Application referencia apenas Domain + SharedKernel + abstrações).
/// </summary>
/// <remarks>
/// Mesmo padrão do <c>UniqueConstraintViolation</c> do cadastro de tipos de
/// deficiência: com dois índices únicos na tabela, devolver o conflito do outro
/// mentiria sobre a causa e apontaria o campo errado ao operador.
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = "ix_recurso_acessibilidade_codigo_vivo";

    private const string NomeConstraint = "ix_recurso_acessibilidade_nome_vivo";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// O conflito da constraint violada quando a exceção é uma <c>DbUpdateException</c>
    /// sobre uma <c>PostgresException</c> 23505 de um dos dois índices;
    /// <see langword="null"/> caso contrário — o caller deixa a exceção propagar.
    /// </summary>
    public static DomainError? ConflitoDeUnicidade(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ConstraintVioladaPor(ex) switch
        {
            CodigoConstraint => CodigoJaExisteErro(),
            NomeConstraint => NomeJaExisteErro(),
            _ => null,
        };
    }

    public static DomainError CodigoJaExisteErro() =>
        new(RecursoAcessibilidadeErrorCodes.CodigoJaExiste,
            "Já existe um recurso de acessibilidade vivo com o código informado.");

    public static DomainError NomeJaExisteErro() =>
        new(RecursoAcessibilidadeErrorCodes.NomeJaExiste,
            "Já existe um recurso de acessibilidade vivo com o nome informado.");

    private static string? ConstraintVioladaPor(Exception ex)
    {
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
}
