namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Contraprova estrutural da fronteira do módulo (ADR-0105), contra o banco real: o
/// schema <c>publicacoes</c> não possui coluna de domínio alheio, e nenhuma das suas
/// chaves estrangeiras atravessa a fronteira de outro módulo.
/// </summary>
/// <remarks>
/// <para>
/// A ausência só vale como evidência se o detector <b>saberia</b> acusar a presença — e
/// se o banco <b>aceitaria</b> o que se afirma não existir. Com schema por módulo no
/// mesmo banco (ADR-0097), uma chave estrangeira de <c>publicacoes</c> para
/// <c>selecao</c> é tecnicamente possível: nada na física a impede, só a decisão de
/// modelagem. Por isso cada teste <b>planta o canário primeiro</b> — cria a violação,
/// verifica que o Postgres a aceita e que a consulta a enxerga —, desfaz, e só então
/// assere a ausência. Sem isso, um detector quebrado (ou um schema vazio) passaria
/// trivialmente, e o teste seria decoração.
/// </para>
/// <para>
/// O canário vive dentro de uma transação desfeita ao fim: no Postgres o DDL é
/// transacional, então o <c>ROLLBACK</c> não deixa rastro no schema real.
/// </para>
/// </remarks>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o canário e os detectores não recebem entrada externa.")]
public sealed class FronteiraDoModuloTests
{
    /// <summary>
    /// Colunas que denunciariam o módulo conhecendo os domínios. A ADR-0105 nomeia estas
    /// três; a lista é a da própria decisão, não uma amostra.
    /// </summary>
    private static readonly string[] ColunasDeDominioAlheio =
        ["processo_seletivo_id", "chamada_id", "aplicacao_prova_id"];

    private const string ContaColunasDeDominio = """
        SELECT count(*)
        FROM information_schema.columns
        WHERE table_schema = 'publicacoes'
          AND column_name = ANY(@proibidas)
        """;

    private const string ListaChavesEstrangeirasCrossSchema = """
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
        JOIN pg_class alvo ON alvo.oid = con.confrelid
        JOIN pg_namespace ns_alvo ON ns_alvo.oid = alvo.relnamespace
        WHERE con.contype = 'f'
          AND ns.nspname = 'publicacoes'
          AND ns_alvo.nspname <> 'publicacoes'
        """;

    private readonly PublicacoesDbFixture _fixture;

    public FronteiraDoModuloTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ADR-0105: o schema publicacoes não tem coluna de domínio alheio (com canário)")]
    public async Task Schema_NaoTemColunaDeDominioAlheio()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Canário: a coluna proibida, plantada — o banco a aceita, e o detector a acusa.
        await using (NpgsqlTransaction canario = await conexao.BeginTransactionAsync())
        {
            await ExecutarAsync(
                conexao,
                canario,
                "CREATE TABLE publicacoes.canario_dominio (id uuid PRIMARY KEY, processo_seletivo_id uuid)");

            long comCanario = await ContarColunasDeDominioAsync(conexao, canario);
            comCanario.Should().Be(
                1, "sem acusar a violação plantada, a ausência de achados sobre o schema real não significaria nada");

            await canario.RollbackAsync();
        }

        long noSchemaReal = await ContarColunasDeDominioAsync(conexao, transacao: null);

        noSchemaReal.Should().Be(
            0,
            "o módulo não conhece ProcessoSeletivo, Chamada nem AplicacaoProva (ADR-0105): "
            + "o vínculo com a entidade é um par opaco, não uma coluna de domínio");
    }

    [Fact(DisplayName = "ADR-0061: nenhuma chave estrangeira de publicacoes atravessa outro schema (com canário)")]
    public async Task Schema_NaoTemChaveEstrangeiraCrossSchema()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Canário: a chave estrangeira cross-schema, plantada. Prova as duas coisas de
        // que a asserção depende — que o banco a aceitaria (com schema por módulo,
        // ADR-0097, nada na física a impede) e que a consulta a enxergaria.
        await using (NpgsqlTransaction canario = await conexao.BeginTransactionAsync())
        {
            await ExecutarAsync(conexao, canario, "CREATE SCHEMA canario_vizinho");
            await ExecutarAsync(conexao, canario, "CREATE TABLE canario_vizinho.alvo (id uuid PRIMARY KEY)");
            await ExecutarAsync(
                conexao,
                canario,
                """
                CREATE TABLE publicacoes.canario_fk (
                    id uuid PRIMARY KEY,
                    alvo_id uuid NOT NULL REFERENCES canario_vizinho.alvo (id)
                )
                """);

            IReadOnlyList<string> comCanario = await ListarChavesCrossSchemaAsync(conexao, canario);
            comCanario.Should().ContainSingle(
                "o Postgres aceita a chave estrangeira cross-schema — a ausência dela é decisão de "
                + "modelagem (ADR-0061), não impedimento físico; e o detector precisa enxergá-la");

            await canario.RollbackAsync();
        }

        IReadOnlyList<string> noSchemaReal = await ListarChavesCrossSchemaAsync(conexao, transacao: null);

        noSchemaReal.Should().BeEmpty(
            "nenhuma chave estrangeira do módulo atravessa a fronteira de outro módulo (ADR-0061): "
            + "a referência cross-módulo é por valor");
    }

    [Fact(DisplayName = "ADR-0105: a única chave estrangeira do vínculo aponta para o próprio ato")]
    public async Task Vinculo_SoTemChaveEstrangeiraParaOAto()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT alvo.relname
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace ns ON ns.oid = rel.relnamespace
            JOIN pg_class alvo ON alvo.oid = con.confrelid
            WHERE con.contype = 'f'
              AND ns.nspname = 'publicacoes'
              AND rel.relname = 'vinculo_ato_entidade'
            """;

        List<string> alvos = [];
        await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync())
        {
            while (await leitor.ReadAsync())
            {
                alvos.Add(leitor.GetString(0));
            }
        }

        alvos.Should().ContainSingle().Which.Should().Be(
            "ato_normativo",
            "o vínculo aponta para o ato e para mais nada: o objeto do outro lado é opaco, "
            + "e uma chave estrangeira para ele reintroduziria o acoplamento que a ADR-0105 recusa");
    }

    private static async Task<long> ContarColunasDeDominioAsync(NpgsqlConnection conexao, NpgsqlTransaction? transacao)
    {
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = ContaColunasDeDominio;
        comando.Parameters.AddWithValue("proibidas", ColunasDeDominioAlheio);

        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private static async Task<IReadOnlyList<string>> ListarChavesCrossSchemaAsync(
        NpgsqlConnection conexao, NpgsqlTransaction? transacao)
    {
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = ListaChavesEstrangeirasCrossSchema;

        List<string> nomes = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            nomes.Add(leitor.GetString(0));
        }

        return nomes;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, string sql)
    {
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
