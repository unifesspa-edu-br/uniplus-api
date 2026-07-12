namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Prova, contra o catálogo do Postgres real, que a <see cref="VersaoConfiguracao"/>
/// referencia o ato criador <b>por valor</b> (ADR-0061/0103/0104): o ato vive em
/// <c>Publicacoes</c>, e nenhuma chave estrangeira liga a versão a ele.
/// </summary>
/// <remarks>
/// Cada teste planta um <b>canário</b> antes de assegurar a ausência: cria a
/// violação dentro de uma transação, confirma que o detector a acusa, faz
/// <c>ROLLBACK</c>, e só então assere ausência no schema real. Sem isso, um
/// detector quebrado passaria como verde — provando nada. (DDL é transacional no
/// Postgres; o canário não deixa rastro.)
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o canário e os detectores não recebem entrada externa.")]
public sealed class FronteiraVersaoConfiguracaoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string ListaChavesEstrangeirasDaTabela = """
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
        WHERE con.contype = 'f'
          AND ns.nspname = 'selecao'
          AND rel.relname = 'versoes_configuracao'
        """;

    private const string ListaChavesEstrangeirasSobreOAto = """
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
        JOIN LATERAL unnest(con.conkey) AS coluna(attnum) ON true
        JOIN pg_attribute att ON att.attrelid = rel.oid AND att.attnum = coluna.attnum
        WHERE con.contype = 'f'
          AND ns.nspname = 'selecao'
          AND rel.relname = 'versoes_configuracao'
          AND att.attname IN ('ato_criador_id', 'ato_criador_retifica_id')
        """;

    private readonly ProcessoSeletivoDbFixture _fixture;

    public FronteiraVersaoConfiguracaoTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "As colunas do ato criador não têm chave estrangeira alguma — a referência é por valor (ADR-0061)")]
    public async Task AtoCriador_NaoTemChaveEstrangeira()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Canário: o banco ACEITARIA uma FK do ato criador para uma tabela local, e o
        // detector a enxergaria. É o que torna a ausência, abaixo, uma decisão — e não
        // um acidente de esquema. (Antes da #804 o alvo do canário era `selecao.editais`;
        // com a tabela eliminada, qualquer tabela do módulo serve — o que se prova é que
        // NÃO HÁ integridade referencial sobre o ato criador, seja para onde for.)
        await using (NpgsqlTransaction canario = await conexao.BeginTransactionAsync())
        {
            await ExecutarAsync(conexao, canario, """
                CREATE TABLE selecao.canario_atos (id uuid PRIMARY KEY);
                """);
            await ExecutarAsync(conexao, canario, """
                ALTER TABLE selecao.versoes_configuracao
                ADD CONSTRAINT canario_fk_ato_criador
                FOREIGN KEY (ato_criador_id) REFERENCES selecao.canario_atos (id)
                NOT VALID
                """);

            IReadOnlyList<string> comCanario = await ListarAsync(conexao, canario, ListaChavesEstrangeirasSobreOAto);
            comCanario.Should().ContainSingle(
                "o canário prova que o banco aceita a FK e que a consulta a detecta");

            await canario.RollbackAsync();
        }

        IReadOnlyList<string> noSchemaReal = await ListarAsync(conexao, transacao: null, ListaChavesEstrangeirasSobreOAto);

        noSchemaReal.Should().BeEmpty(
            "o ato criador é referenciado por VALOR (ADR-0061): ele vive em Publicacoes, e não há "
            + "integridade referencial cruzando a fronteira do módulo");
    }

    [Fact(DisplayName = "A única chave estrangeira da tabela é a do processo — e não sai do schema do módulo")]
    public async Task VersaoConfiguracao_SoTemChaveEstrangeiraParaOProcesso()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        IReadOnlyList<string> chaves = await ListarAsync(conexao, transacao: null, ListaChavesEstrangeirasDaTabela);

        chaves.Should().ContainSingle().Which.Should().Be(
            "fk_versoes_configuracao_processo",
            "a versão pende do certame — e de mais nada; o ato que a criou entra por valor");
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
