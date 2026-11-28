namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Helper para mapear violações 23505 dos índices únicos parciais do código e do nome vivos da
/// modalidade — <c>ux_modalidades_codigo_ativos</c> e <c>ux_modalidades_nome_ativos</c> — para o
/// <c>DomainError</c> apropriado. Acessa <c>SqlState</c> e <c>ConstraintName</c> por reflection no
/// inner exception — o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> na camada Application (mantém Clean Arch).
/// </summary>
/// <remarks>
/// Mesmo padrão do <c>UniqueConstraintViolation</c> dos demais cadastros de
/// Configuração. A tradução cross-cutting de 23505 → 409 via
/// <c>GlobalExceptionMiddleware</c> permanece como follow-up separado (#504); este
/// helper cobre o caso específico da unicidade do código e do nome, inclusive a corrida
/// na criação e, para o nome, também na atualização (o código é imutável, então não há corrida dele na atualização).
/// </remarks>
internal static class UniqueConstraintViolation
{
    private const string UniqueViolationSqlState = "23505";

    private const string CodigoConstraint = RestricoesDoBanco.ModalidadeCodigoAtivos;

    private const string NomeConstraint = RestricoesDoBanco.ModalidadeNomeAtivos;

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

        return string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            && ex.InnerException is { } inner
                ? ConstraintDaViolacaoDeUnicidade(inner)
                : null;
    }

    private static string? ConstraintDaViolacaoDeUnicidade(Exception inner)
    {
        Type innerType = inner.GetType();
        string? sqlState = innerType.GetProperty("SqlState")?.GetValue(inner) as string;

        return string.Equals(sqlState, UniqueViolationSqlState, StringComparison.Ordinal)
            ? innerType.GetProperty("ConstraintName")?.GetValue(inner) as string
            : null;
    }

    /// <summary>
    /// O erro de negócio do índice único violado — código ou nome já existente —, ou <see langword="null"/>
    /// quando a exceção não é violação de nenhum dos dois e deve propagar.
    /// </summary>
    public static DomainError? ErroDoIndiceViolado(Exception ex)
    {
        string? constraint = GetViolatedConstraint(ex);

        if (IsCodigoConflict(constraint))
        {
            return CodigoJaExisteErro();
        }

        return IsNomeConflict(constraint) ? NomeJaExisteErro() : null;
    }

    public static DomainError CodigoJaExisteErro() =>
        new(ModalidadeErrorCodes.CodigoJaExiste, "Já existe uma modalidade viva com o código informado.");

    public static DomainError NomeJaExisteErro() =>
        new(ModalidadeErrorCodes.NomeJaExiste, "Já existe uma modalidade viva com o nome informado.");

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é o índice único parcial
    /// que garante uma única modalidade viva por código.
    /// </summary>
    public static bool IsCodigoConflict(string? constraint) =>
        string.Equals(constraint, CodigoConstraint, StringComparison.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando a constraint violada é o índice único parcial
    /// que garante uma única modalidade viva por nome.
    /// </summary>
    public static bool IsNomeConflict(string? constraint) =>
        string.Equals(constraint, NomeConstraint, StringComparison.Ordinal);
}
