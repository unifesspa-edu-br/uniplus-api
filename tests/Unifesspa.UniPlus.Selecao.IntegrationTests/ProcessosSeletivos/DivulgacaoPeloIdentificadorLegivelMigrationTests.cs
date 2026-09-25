namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova o que a migration que localiza a divulgação pelo identificador legível faz com as
/// divulgações já materializadas na forma anterior da projeção pública.
/// </summary>
/// <remarks>
/// A projeção anterior não tem de onde tirar o identificador — foi feita de envelopes congelados
/// antes de ele existir —, e a coluna nova é obrigatória e única. A migration remove essas linhas;
/// sem isso, ou a coluna nasceria com um valor vazio que o domínio nunca aceitaria, ou a própria
/// migration falharia no índice único.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o seed legado não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class DivulgacaoPeloIdentificadorLegivelMigrationTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260925045605_RegistraIdentificadorLegivelDaVersaoBaseNaRetificacao";

    private const string MigrationSobProva = "20260925055651_LocalizaCertameDivulgadoPeloIdentificadorLegivel";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_divulgacao_identificador_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration remove as divulgações da projeção anterior e cria a coluna obrigatória, sem valor padrão")]
    public async Task Migration_RemoveProjecaoAnterior_ECriaColunaObrigatoriaSemPadrao()
    {
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.GetService<IMigrator>().MigrateAsync(MigrationAnterior);
        }

        await using (NpgsqlConnection conexao = new(_postgres.GetConnectionString()))
        {
            await conexao.OpenAsync();
            await ExecutarAsync(
                conexao,
                """
                INSERT INTO selecao.certames_divulgados
                    (id, numero_versao, ato_criador_id, hash_configuracao, versao_projecao, nome, numero,
                     modalidades_ofertadas, inscricoes_de, inscricoes_ate, certame, divulgado_em)
                VALUES
                    ('11111111-1111-7111-8111-111111111111', 1, '22222222-2222-7222-8222-222222222222',
                     repeat('a', 64), '1', 'Certame da projeção anterior', '001/2026',
                     ARRAY['AC'], now(), now() + interval '10 days', '{"nome":"documento"}', now());
                """);
        }

        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.GetService<IMigrator>().MigrateAsync(MigrationSobProva);
        }

        await using NpgsqlConnection leitura = new(_postgres.GetConnectionString());
        await leitura.OpenAsync();

        (await EscalarAsync<long>(leitura, "SELECT count(*) FROM selecao.certames_divulgados;"))
            .Should().Be(0, "a projeção anterior não tem identificador e não é reprojetável");

        (await EscalarAsync<string>(
            leitura,
            """
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = 'selecao' AND table_name = 'certames_divulgados'
              AND column_name = 'identificador_legivel';
            """)).Should().Be("NO");

        (await EscalarAsync<object>(
            leitura,
            """
            SELECT column_default FROM information_schema.columns
            WHERE table_schema = 'selecao' AND table_name = 'certames_divulgados'
              AND column_name = 'identificador_legivel';
            """)).Should().Be(
                DBNull.Value,
                "um padrão vazio deixaria a coluna aceitar em silêncio uma divulgação sem endereço público");
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<T> EscalarAsync<T>(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    private SelecaoDbContext CriarContexto()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SelecaoDbContext(options);
    }
}
