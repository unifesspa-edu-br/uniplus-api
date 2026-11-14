namespace Unifesspa.UniPlus.Application.Abstractions.Persistence;

/// <summary>
/// Extrai o nome da constraint de banco que uma gravação violou (índice UNIQUE ou CHECK), para que quem grava a traduza
/// em recusa nomeada.
/// </summary>
/// <remarks>
/// O tipo da exceção é inspecionado por <see cref="Type.FullName"/> e <c>SqlState</c> e <c>ConstraintName</c> são lidos
/// por reflexão da exceção interna, cujo formato é estável na API pública do <c>Npgsql.PostgresException</c>: a camada de
/// aplicação não passa a depender dos pacotes do EF Core nem do Npgsql.
/// </remarks>
public static class ConstraintVioladaNaGravacao
{
    private const string ViolacaoDeUnicidade = "23505";
    private const string ViolacaoDeCheck = "23514";
    private const string TipoDaExcecaoDeGravacao = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>
    /// O nome da constraint violada quando a exceção é uma <c>DbUpdateException</c> sobre uma violação de unicidade
    /// (23505) ou de CHECK (23514); <see langword="null"/> caso contrário, e então quem chama deve propagá-la.
    /// </summary>
    public static string? NomeDaConstraintViolada(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        if (!string.Equals(excecao.GetType().FullName, TipoDaExcecaoDeGravacao, StringComparison.Ordinal)
            || excecao.InnerException is not { } interna)
        {
            return null;
        }

        Type tipoInterno = interna.GetType();
        string? estado = tipoInterno.GetProperty("SqlState")?.GetValue(interna) as string;

        return estado is ViolacaoDeUnicidade or ViolacaoDeCheck
            ? tipoInterno.GetProperty("ConstraintName")?.GetValue(interna) as string
            : null;
    }
}
