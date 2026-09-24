namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova executável da precondição da migration que semeia o critério de desempate por área
/// do ENEM: o <c>Down</c> só remove a entrada enquanto nenhuma configuração congelada a
/// referenciar (ADR-0112), e olha só a própria entrada.
/// </summary>
/// <remarks>
/// Fixture próprio: a <c>VersaoConfiguracao</c> fabricada é append-only por gatilho e não pode
/// ser removida, então uma referência fabricada aqui bloquearia a precondição provada por outra
/// classe se as duas dividissem o banco.
/// </remarks>
public sealed class DesempatePorAreaDoEnemPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public DesempatePorAreaDoEnemPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private const string ArquivoDaMigration = "20260924075815_SemeiaDesempatePorAreaDoEnem.cs";

    private const string PredicadoDaEntrada =
        """@.codigo == "DESEMPATE-MAIOR-NOTA-AREA-ENEM" && @.versao == "v1" && exists(@.hash)""";

    /// <summary>O mesmo bloco do <c>Down</c> da migration: nomeia só a entrada que ela remove.</summary>
    private const string PrecondicaoDaMigration = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "DESEMPATE-MAIOR-NOTA-AREA-ENEM" && @.versao == "v1" && exists(@.hash))'
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: critério de desempate por área do ENEM referenciado por versão de configuração congelada; remover viola o append-only (ADR-0112)';
            END IF;
        END
        $adr0112$;
        """;

    [Fact(DisplayName = "A precondição olha só a própria entrada e aborta apenas diante da referência real")]
    public async Task Precondicao_EscopadaNaPropriaEntrada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Task ReverterAsync() => FronteiraAppendOnlyDoRol.ExecutarAsync(context, PrecondicaoDaMigration);

        // Sem referência alguma, reverter é legítimo: a entrada ainda é vocabulário, não fato.
        await ReverterAsync();

        // Referência congelada a OUTRO critério de desempate não bloqueia esta reversão.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outro-criterio",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(CriterioDesempateCodigo.MaiorNotaEtapa, "v1"));
        await ReverterAsync();

        // Nem referência a uma versão que este Down não remove.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outra-versao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v2"));
        await ReverterAsync();

        // Nem um homônimo: objeto com a chave bare `codigo` de mesmo valor, sem a tripla.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-fase-homonima",
            FronteiraAppendOnlyDoRol.FaseHomonima(CriterioDesempateCodigo.MaiorNotaAreaEnem));
        await ReverterAsync();

        // A referência real, sim: a partir dela a entrada é fato, e a precondição aborta antes
        // de alterar qualquer linha.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "referencia-real",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(CriterioDesempateCodigo.MaiorNotaAreaEnem, "v1"));

        Func<Task> reversao = ReverterAsync;

        (await reversao.Should().ThrowAsync<DbException>(
            "a precondição da migration aborta diante da referência congelada"))
            .WithMessage("*ADR-0112*");
    }

    [Fact(DisplayName = "O Down da migration carrega a precondição — o SQL provado aqui é o executado lá")]
    public void MigrationDown_CarregaAPrecondicao()
    {
        string guarda = FronteiraAppendOnlyDoRol.BlocoDown(
            FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration));

        foreach (string marca in new[] { "$adr0112$", "RAISE EXCEPTION", PredicadoDaEntrada })
        {
            guarda.Should().Contain(
                marca,
                "sem a precondição no Down, a prova comportamental desta classe deixaria de refletir a migration real");
        }

        guarda.Should().NotContain(
            CriterioDesempateCodigo.MaiorNotaEtapa,
            "a guarda não pode olhar outro critério de desempate, que esta migration não remove");
    }
}
