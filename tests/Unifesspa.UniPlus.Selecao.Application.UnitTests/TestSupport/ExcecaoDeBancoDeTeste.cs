namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;

/// <summary>A exceção que o EF devolve quando o Postgres recusa a gravação por violar um índice único.</summary>
internal static class ExcecaoDeBancoDeTeste
{
    private const string EstadoDeUnicidadeViolada = "23505";

    public static DbUpdateException ViolacaoDeUnicidade(string constraint) =>
        new("Falha ao gravar.", new ErroDoBanco(EstadoDeUnicidadeViolada, constraint));

    private sealed class ErroDoBanco(string sqlState, string constraintName) : Exception("Violação de índice único.")
    {
        public string SqlState { get; } = sqlState;

        public string ConstraintName { get; } = constraintName;
    }
}
