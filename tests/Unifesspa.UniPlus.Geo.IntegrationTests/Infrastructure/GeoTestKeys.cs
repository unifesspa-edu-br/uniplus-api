namespace Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Utilitários para os testes de integração do Geo, que compartilham um único
/// Postgres por coleção (<see cref="GeoPostgisFixture"/>). Gera chaves naturais
/// únicas por chamada — assim nenhum teste colide nos índices UNIQUE com outro
/// teste da mesma execução, qualquer que seja a ordem — e centraliza a verificação
/// de que a constraint <em>certa</em> falhou (não um erro qualquer de banco).
/// </summary>
internal static class GeoTestKeys
{
    private static int _codigoSeq = 1_000_000;

    /// <summary>
    /// Sufixo único (hex). Usa a porção <strong>aleatória</strong> do Guid v7 (32
    /// chars hex), não o prefixo: os 12 primeiros hex de um Guid v7 são o timestamp
    /// em ms — iguais para Guids criados no mesmo milissegundo, o que colidiria.
    /// </summary>
    public static string Token() => Guid.CreateVersion7().ToString("N")[12..].ToUpperInvariant();

    /// <summary>Sigla ISO única — coluna é <c>text</c>, tamanho livre em teste.</summary>
    public static string SiglaIso() => "ISO" + Token();

    /// <summary>UF única.</summary>
    public static string Uf() => "UF" + Token();

    /// <summary>Código IBGE único de 7 dígitos (contador atômico — garante unicidade entre chamadas).</summary>
    public static string CodigoIbge() =>
        Interlocked.Increment(ref _codigoSeq).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Desempacota um <see cref="Result{T}"/> de factory, falhando o teste com a mensagem de erro.</summary>
    public static T Ok<T>(Result<T> resultado)
    {
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    /// <summary>
    /// Verifica que a exceção é uma violação de UNIQUE (SQLSTATE 23505) na constraint
    /// esperada — prova que a duplicata foi barrada pelo índice certo, não por outro erro.
    /// </summary>
    public static void DeveSerViolacaoUnique(Exception excecao, string constraintEsperada)
    {
        excecao.Should().BeOfType<DbUpdateException>();
        PostgresException? pg = excecao.InnerException as PostgresException;
        pg.Should().NotBeNull("a causa deve ser uma PostgresException");
        pg!.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation, "deve ser unique_violation (23505)");
        pg.ConstraintName.Should().Be(constraintEsperada);
    }
}
