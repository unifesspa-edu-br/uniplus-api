namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

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
    private const string SchemasDeModulo = "'configuracao','organizacao','selecao','ingresso','publicacoes'";

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

    private readonly MonolitoPostgresFixture _fixture;

    public IsolamentoCrossSchemaTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Canário: a chave estrangeira cross-schema é possível, e a consulta de detecção a encontra")]
    public async Task Canario_ChaveEstrangeiraCrossSchemaEDetectada()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();

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
        OrganizacaoInstitucionalDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();

        IReadOnlyCollection<string> violacoes = await ListarViolacoesAsync(dbContext);

        violacoes.Should().BeEmpty(
            "referência cross-módulo é por valor (ADR-0061), nunca por chave estrangeira. Com schema-por-módulo "
            + "a chave estrangeira é tecnicamente possível — quem a impede é o modelo, e é isto que este teste "
            + "defende. O canário prova que a consulta acima detecta a violação quando ela existe.");
    }

    private static async Task<IReadOnlyCollection<string>> ListarViolacoesAsync(DbContext dbContext)
    {
        return await dbContext.Database
            .SqlQueryRaw<string>(ConsultaViolacoes)
            .ToListAsync(CancellationToken.None);
    }
}
