namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// Nenhuma chave estrangeira atravessa a fronteira de um módulo (ADR-0061).
///
/// <para>Este teste mudou de natureza com a topologia de banco único e
/// schema-por-módulo (ADR-0097). Enquanto cada módulo tinha o seu banco, a
/// ausência de chave estrangeira cross-módulo era garantida pela FÍSICA: no
/// PostgreSQL uma chave estrangeira não referencia tabela de outro banco. Um
/// teste sobre isso provaria apenas o que o motor já garante.</para>
///
/// <para>Com schema-por-módulo, <c>REFERENCES selecao.processos_seletivos(id)</c>
/// a partir de <c>publicacoes</c> FUNCIONA. A trava deixa de ser física e passa a
/// ser decisão de modelagem. Por isso o teste planta um CANÁRIO: cria a chave
/// estrangeira cross-schema, exige que a consulta de detecção a encontre, remove-a,
/// e só então assere que nenhuma real existe. Sem esse passo, uma consulta quebrada
/// — ou um schema recém-criado e ainda vazio — passaria silenciosamente.</para>
///
/// <para>O catálogo também exige que toda tabela esteja no schema de um módulo, da plataforma ou do Wolverine
/// (ADR-0170): nada em <c>public</c>. O canário planta uma tabela fora dos schemas declarados e exige que a
/// consulta a encontre.</para>
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class IsolamentoCrossSchemaTests
{
    /// <summary>
    /// Roster explícito dos schemas de módulo. Não usar "todos menos X": a varredura
    /// pegaria <c>wolverine</c>, <c>public</c>, <c>information_schema</c> e schemas de
    /// extensão, que não são módulos e têm regras próprias.
    /// </summary>
    private static readonly string[] EsquemasDeModulo =
        ["autorizacao", "configuracao", "discentes", "ingresso", "organizacao", "publicacoes", "selecao"];

    /// <summary>Os schemas que não são de módulo e podem ter tabela: a plataforma (idempotência e histórico de migrations) e o outbox do Wolverine.</summary>
    private static readonly string[] EsquemasDaInfraestrutura = ["plataforma", "wolverine", "wolverine_queues"];

    private static readonly string SchemasDeModulo = Lista(EsquemasDeModulo);

    /// <summary>
    /// Chaves estrangeiras cuja tabela de origem está num schema de módulo e cuja
    /// tabela referenciada está em OUTRO schema.
    /// </summary>
    private static readonly string ConsultaViolacoes = string.Format(
        CultureInfo.InvariantCulture,
        """
        SELECT (origem.nspname || '.' || tabela_origem.relname || ' -> '
                || destino.nspname || '.' || tabela_destino.relname) AS "Value"
        FROM pg_constraint c
        JOIN pg_class     tabela_origem  ON tabela_origem.oid  = c.conrelid
        JOIN pg_namespace origem         ON origem.oid         = tabela_origem.relnamespace
        JOIN pg_class     tabela_destino ON tabela_destino.oid = c.confrelid
        JOIN pg_namespace destino        ON destino.oid        = tabela_destino.relnamespace
        WHERE c.contype = 'f'
          AND origem.nspname IN ({0})
          AND destino.nspname <> origem.nspname
        """,
        SchemasDeModulo);

    /// <summary>
    /// Tabelas (comuns e particionadas) fora dos schemas declarados. Os schemas do sistema do PostgreSQL não entram; qualquer
    /// tabela em <c>public</c> entra, porque o modelo não cria nenhuma lá e extensão que crie tabela precisa ser declarada.
    /// </summary>
    private static readonly string ConsultaTabelasForaDoCatalogo = string.Format(
        CultureInfo.InvariantCulture,
        """
        SELECT (esquema.nspname || '.' || tabela.relname) AS "Value"
        FROM pg_class tabela
        JOIN pg_namespace esquema ON esquema.oid = tabela.relnamespace
        WHERE tabela.relkind IN ('r', 'p')
          AND esquema.nspname NOT IN ({0})
          AND esquema.nspname <> 'information_schema'
          AND esquema.nspname NOT LIKE 'pg\_%'
        """,
        Lista([.. EsquemasDeModulo, .. EsquemasDaInfraestrutura]));

    private static readonly string ConsultaEsquemasDeModuloSemTabela = string.Format(
        CultureInfo.InvariantCulture,
        """
        SELECT esquema AS "Value"
        FROM unnest(ARRAY[{0}]) AS esquema
        WHERE NOT EXISTS (
            SELECT 1 FROM pg_class tabela
            JOIN pg_namespace ns ON ns.oid = tabela.relnamespace
            WHERE ns.nspname = esquema AND tabela.relkind IN ('r', 'p'))
        """,
        SchemasDeModulo);

    private readonly MonolitoPostgresFixture _fixture;

    public IsolamentoCrossSchemaTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Canário: a chave estrangeira cross-schema é possível, e a consulta de detecção a encontra")]
    public async Task Canario_ChaveEstrangeiraCrossSchemaEDetectada()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        MonolitoDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MonolitoDbContext>();

        try
        {
            // (1) Planta o canário. Se este comando falhasse, a asserção de ausência
            //     seria vácua — estaria provando uma impossibilidade do motor, não do modelo.
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE publicacoes._canario_fk_cross_schema (
                    id                   uuid PRIMARY KEY,
                    processo_seletivo_id uuid NOT NULL
                        REFERENCES selecao.processos_seletivos(id)
                );
                """,
                CancellationToken.None);

            // (2) A consulta de detecção precisa ENXERGAR o canário. É isto que prova
            //     que ela funciona; sem este passo, uma consulta errada passaria calada.
            IReadOnlyCollection<string> comCanario = await ListarViolacoesAsync(dbContext);

            comCanario.Should().ContainSingle(
                violacao => violacao.StartsWith("publicacoes._canario_fk_cross_schema", StringComparison.Ordinal),
                "a consulta de detecção deve encontrar a chave estrangeira cross-schema que acabou de ser criada; "
                + "se não encontra, a asserção de ausência do outro teste não vale como evidência");
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "DROP TABLE IF EXISTS publicacoes._canario_fk_cross_schema;",
                CancellationToken.None);
        }
    }

    [Fact(DisplayName = "ADR-0061: nenhuma chave estrangeira do modelo atravessa schema de módulo")]
    public async Task Modelo_NaoTemChaveEstrangeiraCrossSchema()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        MonolitoDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MonolitoDbContext>();

        IReadOnlyCollection<string> violacoes = await ListarViolacoesAsync(dbContext);

        violacoes.Should().BeEmpty(
            "referência cross-módulo é por valor (ADR-0061), nunca por chave estrangeira. Com schema-por-módulo "
            + "a chave estrangeira é tecnicamente possível — quem a impede é o modelo, e é isto que este teste "
            + "defende. O canário prova que a consulta acima detecta a violação quando ela existe.");
    }

    [Fact(DisplayName = "Canário: a tabela fora dos schemas declarados existe, e a consulta do catálogo a encontra")]
    public async Task Canario_TabelaForaDoCatalogoEDetectada()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        MonolitoDbContext dbContext = scope.ServiceProvider.GetRequiredService<MonolitoDbContext>();

        try
        {
            await ExecutarAsync(dbContext, "CREATE TABLE public._canario_tabela_fora_do_catalogo (id uuid PRIMARY KEY);");

            (await ListarAsync(dbContext, ConsultaTabelasForaDoCatalogo)).Should()
                .ContainSingle(tabela => tabela == "public._canario_tabela_fora_do_catalogo", "sem encontrá-la, a ausência do outro teste não vale");
        }
        finally
        {
            await ExecutarAsync(dbContext, "DROP TABLE IF EXISTS public._canario_tabela_fora_do_catalogo;");
        }
    }

    [Fact(DisplayName = "ADR-0170: toda tabela está no schema de um módulo, da plataforma ou do Wolverine")]
    public async Task Modelo_TodaTabelaEstaNoSchemaDeUmModuloOuDaPlataforma()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        MonolitoDbContext dbContext = scope.ServiceProvider.GetRequiredService<MonolitoDbContext>();

        (await ListarAsync(dbContext, ConsultaTabelasForaDoCatalogo)).Should().BeEmpty(
            "toda tabela pertence ao schema de um módulo ou da plataforma; nada em public além do que o catálogo declarar");
        (await ListarAsync(dbContext, ConsultaEsquemasDeModuloSemTabela)).Should().BeEmpty(
            "o roster tem de corresponder aos schemas que o modelo cria: um schema do roster sem tabela indica roster desatualizado");
    }

    private static async Task<IReadOnlyCollection<string>> ListarViolacoesAsync(DbContext dbContext) =>
        await ListarAsync(dbContext, ConsultaViolacoes);

    private static async Task ExecutarAsync(DbContext dbContext, string comando) =>
        await dbContext.Database.ExecuteSqlRawAsync(comando, CancellationToken.None);

    private static async Task<IReadOnlyCollection<string>> ListarAsync(DbContext dbContext, string consulta) =>
        await dbContext.Database.SqlQueryRaw<string>(consulta).ToListAsync(CancellationToken.None);

    private static string Lista(IEnumerable<string> esquemas) => string.Join(',', esquemas.Select(static e => $"'{e}'"));
}
