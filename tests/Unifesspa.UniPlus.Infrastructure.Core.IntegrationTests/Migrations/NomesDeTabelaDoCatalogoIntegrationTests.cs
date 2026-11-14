namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Migrations;

using AwesomeAssertions;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O nome de toda tabela do banco subido pela preparação do contexto único segue o <c>docs/guia-banco-de-dados.md</c>:
/// <c>snake_case</c>, núcleo no plural e até 30 caracteres. A regra é a de <see cref="RegraDeNomeDeTabela"/>, sem lista de
/// exceções por tabela; a tabela canária no singular prova que a conferência recusa.
/// </summary>
public sealed class NomesDeTabelaDoCatalogoIntegrationTests : IAsyncLifetime
{
    // Tabela de controle do EF Core, criada pela ferramenta e não pelo mapeamento do modelo.
    private const string HistoricoDeMigrations = "__EFMigrationsHistory";

    private const string SqlDasTabelas =
        """
        SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p') AND n.nspname NOT LIKE 'pg\_%' AND n.nspname <> 'information_schema' AND c.relname <> @historico
        """;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Toda tabela do banco subido tem o núcleo no plural, snake_case e até 30 caracteres")]
    public async Task SubirOBanco_TodaTabela_SegueARegraDeNome()
    {
        string conexao = _postgres.GetConnectionString();
        await PreparacaoDoBancoIntegrationTests.SubirAsync(conexao);

        IReadOnlyList<(string Esquema, string Tabela)> tabelas = await TabelasAsync(conexao);

        tabelas.Should().NotBeEmpty("sem tabela criada, a conferência dos nomes passaria vazia");
        IReadOnlyList<string> violacoes = RegraDeNomeDeTabela.Violacoes(tabelas);
        violacoes.Should().BeEmpty("o nome de tabela segue o docs/guia-banco-de-dados.md. Violações:\n" + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "Tabela canária com o núcleo no singular é recusada e, removida, o catálogo volta a conferir")]
    public async Task SubirOBanco_TabelaCanariaNoSingular_EhRecusada()
    {
        string conexao = _postgres.GetConnectionString();
        await PreparacaoDoBancoIntegrationTests.SubirAsync(conexao);

        await ExecutarAsync(conexao, "CREATE TABLE selecao.ato_normativo (id uuid PRIMARY KEY)");
        IReadOnlyList<string> comCanaria = RegraDeNomeDeTabela.Violacoes(await TabelasAsync(conexao));
        await ExecutarAsync(conexao, "DROP TABLE selecao.ato_normativo");
        IReadOnlyList<string> semCanaria = RegraDeNomeDeTabela.Violacoes(await TabelasAsync(conexao));

        comCanaria.Should().Equal("selecao.ato_normativo: o núcleo 'ato' não está no plural");
        semCanaria.Should().BeEmpty();
    }

    private static async Task<IReadOnlyList<(string Esquema, string Tabela)>> TabelasAsync(string conexao)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(SqlDasTabelas, banco);
        comando.Parameters.AddWithValue("historico", HistoricoDeMigrations);

        List<(string, string)> tabelas = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            tabelas.Add((leitor.GetString(0), leitor.GetString(1)));
        }

        return tabelas;
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(sql, banco);
        await comando.ExecuteNonQueryAsync();
    }
}
