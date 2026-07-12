namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Npgsql;

/// <summary>
/// Fitness test da #804 (ADR-0103), contra o catálogo do Postgres real: <b>nenhum índice
/// nem CHECK do schema <c>selecao</c> carrega um literal de tipo de ato no filtro</b>, e o
/// enum de natureza não existe mais na persistência. Acrescentar um tipo de ato passa a ser
/// linha de cadastro — sem migration, sem deploy.
/// </summary>
/// <remarks>
/// <para>
/// São <b>dois</b> detectores, porque o defeito tem duas formas. A que existia era
/// <b>numérica</b>: <c>ux_editais_processo_abertura_unica</c> filtrava por
/// <c>natureza = 1</c> — o valor do enumerado escrito no banco. A que poderia surgir é
/// <b>textual</b>: <c>WHERE tipo_codigo = 'CONVOCACAO'</c>. Um detector que só procurasse
/// strings passaria em branco justamente pelo defeito original.
/// </para>
/// <para>
/// <b>Canário primeiro.</b> Cada teste planta a violação numa transação, confirma que o
/// detector a acusa, faz <c>ROLLBACK</c> e só então assere a ausência sobre o schema real.
/// Sem isso, um detector quebrado passaria como verde — provando nada. A tabela do canário é
/// criada <b>no schema <c>selecao</c></b>, e não em <c>pg_temp</c>: uma tabela temporária
/// não está no escopo varrido, e o canário não exercitaria a mesma consulta (DDL é
/// transacional no Postgres; o rollback não deixa rastro).
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o canário e os detectores não recebem entrada externa.")]
public sealed class SemLiteralDeTipoDeAtoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    /// <summary>
    /// Detector TEXTUAL: predicados de índices parciais e de CHECKs que comparam com um
    /// literal em UPPER_SNAKE. É assim que um tipo de ato apareceria hoje —
    /// <c>TipoAtoPublicado.Codigo</c> é UPPER_SNAKE por invariante do próprio cadastro.
    /// </summary>
    private const string ListaPredicadosComLiteralDeTipo = """
        SELECT ix.relname AS objeto, pg_get_expr(i.indpred, i.indrelid) AS predicado
        FROM pg_index i
        JOIN pg_class ix ON ix.oid = i.indexrelid
        JOIN pg_class t ON t.oid = i.indrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao'
          AND i.indpred IS NOT NULL
          AND pg_get_expr(i.indpred, i.indrelid) ~ '''[A-Z]+(_[A-Z]+)*'''
        UNION ALL
        SELECT con.conname AS objeto, pg_get_constraintdef(con.oid) AS predicado
        FROM pg_constraint con
        JOIN pg_class t ON t.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao'
          AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid) ~ '''[A-Z]+(_[A-Z]+)*'''
        """;

    /// <summary>
    /// Detector do ENUM: qualquer coluna chamada <c>natureza</c> no schema. É a forma
    /// NUMÉRICA do defeito — o enum escrito no banco, que os índices comparavam com um
    /// inteiro. Sem a coluna, o literal numérico não tem onde existir.
    /// </summary>
    private const string ListaColunasDeNatureza = """
        SELECT t.relname || '.' || att.attname
        FROM pg_attribute att
        JOIN pg_class t ON t.oid = att.attrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao'
          AND t.relkind = 'r'
          AND att.attnum > 0
          AND NOT att.attisdropped
          AND att.attname = 'natureza'
        """;

    private const string ListaTabelas = """
        SELECT t.relname
        FROM pg_class t
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao' AND t.relkind = 'r'
        """;

    private readonly ProcessoSeletivoDbFixture _fixture;

    public SemLiteralDeTipoDeAtoTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O enum de natureza não existe na persistência — nem a coluna, nem a tabela que a carregava")]
    public async Task Natureza_NaoExisteNoSchema()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Canário: o detector ENXERGA uma coluna `natureza` quando ela existe.
        await using (NpgsqlTransaction canario = await conexao.BeginTransactionAsync())
        {
            await ExecutarAsync(conexao, canario, """
                CREATE TABLE selecao.canario_editais (id uuid PRIMARY KEY, natureza integer NOT NULL);
                """);

            (await ListarAsync(conexao, canario, ListaColunasDeNatureza))
                .Should().ContainSingle("o canário prova que a consulta acha a coluna quando ela existe");

            await canario.RollbackAsync();
        }

        (await ListarAsync(conexao, transacao: null, ListaColunasDeNatureza))
            .Should().BeEmpty(
                "retificação é uma RELAÇÃO entre atos, não um valor de enum: a marca é a presença do "
                + "ato emendado, e o tipo do ato vem do cadastro de Publicações (ADR-0103)");

        (await ListarAsync(conexao, transacao: null, ListaTabelas))
            .Should().NotContain(
                "editais",
                "o documento normativo é o ato publicado, e vive em Publicacoes — a Seleção o referencia por valor");
    }

    [Fact(DisplayName = "Nenhum índice nem CHECK do schema compara com literal de tipo de ato — acrescentar um tipo é linha de cadastro")]
    public async Task IndicesEChecks_NaoCarregamLiteralDeTipoDeAto()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Canário: planta as DUAS formas do defeito — o índice parcial filtrado por um
        // código de tipo e o CHECK que o compara — e exige que o detector acuse ambas.
        await using (NpgsqlTransaction canario = await conexao.BeginTransactionAsync())
        {
            await ExecutarAsync(conexao, canario, """
                CREATE TABLE selecao.canario_atos (
                    id uuid PRIMARY KEY,
                    processo_seletivo_id uuid NOT NULL,
                    tipo_codigo text NOT NULL,
                    CONSTRAINT canario_ck_tipo CHECK (tipo_codigo <> 'CONVOCACAO')
                );
                """);
            await ExecutarAsync(conexao, canario, """
                CREATE UNIQUE INDEX canario_ux_abertura_unica
                ON selecao.canario_atos (processo_seletivo_id)
                WHERE tipo_codigo = 'EDITAL_ABERTURA';
                """);

            IReadOnlyList<string> acusados = await ListarAsync(conexao, canario, ListaPredicadosComLiteralDeTipo);
            acusados.Should().Contain("canario_ux_abertura_unica", "o detector tem de acusar o índice parcial plantado");
            acusados.Should().Contain("canario_ck_tipo", "e também o CHECK plantado");

            await canario.RollbackAsync();
        }

        (await ListarAsync(conexao, transacao: null, ListaPredicadosComLiteralDeTipo))
            .Should().BeEmpty(
                "nenhum índice ou verificação do módulo enumera tipos de ato: quando a Habilitação chegar, "
                + "criar CONVOCACAO, HOMOLOGACAO_ANALISE e LISTA_ESPERA deve ser INSERT no cadastro — "
                + "se exigir migration, esta story foi mal feita (ADR-0103)");
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, NpgsqlTransaction? transacao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> ListarAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction? transacao,
        string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();

        List<string> nomes = [];
        while (await leitor.ReadAsync())
        {
            nomes.Add(leitor.GetString(0));
        }

        return nomes;
    }
}
