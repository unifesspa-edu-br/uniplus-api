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
    /// Detector TEXTUAL: predicados de índices parciais e de CHECKs que comparam a coluna do
    /// código do tipo de ato (<c>tipo_codigo</c> ou <c>ato_codigo</c>) com um literal em
    /// UPPER_SNAKE. É assim que um tipo de ato apareceria hoje — <c>TipoAtoPublicado.Codigo</c> é
    /// UPPER_SNAKE por invariante do próprio cadastro. O vocabulário fechado de um enum de contrato,
    /// também em UPPER_SNAKE, vive em outras colunas e não é tipo de ato.
    /// </summary>
    private const string ListaPredicadosComLiteralDeTipo = """
        SELECT ix.relname AS objeto, pg_get_expr(i.indpred, i.indrelid) AS predicado
        FROM pg_index i
        JOIN pg_class ix ON ix.oid = i.indexrelid
        JOIN pg_class t ON t.oid = i.indrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao'
          AND i.indpred IS NOT NULL
          AND pg_get_expr(i.indpred, i.indrelid) ~ '(ato_codigo|tipo_codigo)[^'']*''[A-Z]+(_[A-Z]+)*'''
        UNION ALL
        SELECT con.conname AS objeto, pg_get_constraintdef(con.oid) AS predicado
        FROM pg_constraint con
        JOIN pg_class t ON t.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'selecao'
          AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid) ~ '(ato_codigo|tipo_codigo)[^'']*''[A-Z]+(_[A-Z]+)*'''
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

    private const string CriarAtosDeExemplo = """
        CREATE TABLE selecao.canario_atos (
            id uuid PRIMARY KEY,
            processo_seletivo_id uuid NOT NULL,
            tipo_codigo text NOT NULL,
            CONSTRAINT canario_ck_tipo CHECK (tipo_codigo <> 'CONVOCACAO')
        );
        CREATE UNIQUE INDEX canario_ux_abertura_unica
        ON selecao.canario_atos (processo_seletivo_id)
        WHERE tipo_codigo = 'EDITAL_ABERTURA';
        """;

    [Fact(DisplayName = "A consulta identifica uma coluna de natureza")]
    public async Task Consulta_ComColunaDeNatureza_IdentificaColuna()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await ExecutarAsync(conexao, transacao, "CREATE TABLE selecao.canario_editais (id uuid PRIMARY KEY, natureza integer NOT NULL);");
        (await ListarAsync(conexao, transacao, ListaColunasDeNatureza)).Should().ContainSingle();
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O enum de natureza não existe no schema")]
    public async Task Natureza_NaoExisteNoSchema()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        (await ListarAsync(conexao, null, ListaColunasDeNatureza)).Should().BeEmpty("o tipo do ato vem do cadastro de Publicações (ADR-0103)");
        (await ListarAsync(conexao, null, ListaTabelas)).Should().NotContain("editais", "o ato publicado pertence a Publicações");
    }

    [Fact(DisplayName = "A consulta identifica literais de tipo de ato em índices e verificações")]
    public async Task Consulta_ComLiteraisDeTipoDeAto_IdentificaIndiceEVerificacao()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await ExecutarAsync(conexao, transacao, CriarAtosDeExemplo);
        IReadOnlyList<string> encontrados = await ListarAsync(conexao, transacao, ListaPredicadosComLiteralDeTipo);
        encontrados.Should().Contain("canario_ux_abertura_unica");
        encontrados.Should().Contain("canario_ck_tipo");
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "Índices e verificações não enumeram tipos de ato")]
    public async Task IndicesEChecks_NaoCarregamLiteralDeTipoDeAto()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        (await ListarAsync(conexao, null, ListaPredicadosComLiteralDeTipo))
            .Should().BeEmpty("os tipos de ato pertencem ao cadastro de Publicações (ADR-0103)");
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
