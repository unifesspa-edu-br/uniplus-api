namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using System.Reflection;

using Npgsql;

/// <summary>
/// Constrói <see cref="PostgresException"/> sintéticas para testar a tradução de
/// exceção em <c>ExclusionConstraintViolation</c>/<c>UniqueConstraintViolation</c>
/// sem precisar de um Postgres real. <c>ConstraintName</c> só tem setter privado no
/// driver (populado normalmente a partir da mensagem de erro do protocolo) — o
/// backing field é setado via reflection, técnica restrita a este helper de teste.
/// </summary>
public static class PostgresExceptionFactory
{
    public static PostgresException Create(string sqlState, string? constraintName = null)
    {
        var ex = new PostgresException("ERROR", "ERROR", "mensagem sintética de teste", sqlState);

        if (constraintName is not null)
        {
            FieldInfo field = typeof(PostgresException)
                .GetField("<ConstraintName>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Backing field de ConstraintName não encontrado — API do Npgsql mudou.");
            field.SetValue(ex, constraintName);
        }

        return ex;
    }
}
