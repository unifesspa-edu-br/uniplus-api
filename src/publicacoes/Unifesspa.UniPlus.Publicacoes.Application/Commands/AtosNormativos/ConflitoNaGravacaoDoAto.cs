namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Traduz a violação de unicidade (<c>23505</c>) que a gravação do ato levanta em recusa nomeada.
/// É a garantia dura contra a corrida check-then-act das consultas prévias: entre a consulta e o
/// <c>SaveChanges</c> cabe outra transação, e só o banco a vê.
/// </summary>
/// <remarks>
/// Este tipo é o dono dos nomes das constraints de Publicações: a linearidade da cadeia de
/// retificação (ADR-0103) e a vaga do objeto (ADR-0107). Acessa <c>SqlState</c> e
/// <c>ConstraintName</c> por reflection no inner exception; o shape é estável na API pública do
/// <c>Npgsql.PostgresException</c>. A inspeção do tipo da exceção por
/// <see cref="System.Type.FullName"/> evita dependência direta do pacote
/// <c>Microsoft.EntityFrameworkCore</c> nesta camada.
/// </remarks>
internal static class ConflitoNaGravacaoDoAto
{
    private const string UniqueViolationSqlState = "23505";

    private const string LinearidadeConstraint = "ux_ato_normativo_retificado";

    private const string LinhagemUnicaConstraint = "ux_linhagem_unica_por_objeto";

    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    private const string RaizJaRetificadaConcorrenteMensagem =
        "O ato retificado já foi retificado por outro ato registrado concorrentemente. "
        + "A cadeia é linear: retifique a cabeça da cadeia.";

    private const string VagaOcupadaConcorrenteMensagem =
        "A vaga do objeto vinculado foi ocupada por um ato registrado concorrentemente. "
        + "O tipo admite um único ato vivo por objeto — consulte os atos da entidade "
        + "antes de reenviar.";

    /// <summary>
    /// A recusa que corresponde à violação, ou <see langword="null"/> quando a exceção não é uma
    /// violação das constraints do ato: quem chama deve propagá-la.
    /// </summary>
    public static DomainError? Traduzir(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ConstraintViolada(ex) switch
        {
            LinearidadeConstraint => new DomainError(
                AtoNormativoErrorCodes.RaizJaRetificada, RaizJaRetificadaConcorrenteMensagem),
            LinhagemUnicaConstraint => new DomainError(
                AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo, VagaOcupadaConcorrenteMensagem),
            _ => null,
        };
    }

    private static string? ConstraintViolada(Exception ex)
    {
        if (!string.Equals(ex.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            || ex.InnerException is not { } inner)
        {
            return null;
        }

        Type innerType = inner.GetType();
        return innerType.GetProperty("SqlState")?.GetValue(inner) as string == UniqueViolationSqlState
            ? innerType.GetProperty("ConstraintName")?.GetValue(inner) as string
            : null;
    }
}
