namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova executável da precondição da migration que semeia a convenção que
/// avança data útil (#1142). O ponto específico desta classe é o <b>escopo</b>:
/// cada migration guarda apenas as entradas que ela mesma remove, então uma
/// referência congelada a outra convenção de contagem, ou a uma versão que
/// aquele <c>Down</c> não apaga, não pode bloqueá-la — se bloqueasse, o
/// predicado estaria ampliado além do que a migration toca.
/// </summary>
/// <remarks>
/// Fixture próprio, e não o da prova da migration anterior: a
/// <c>VersaoConfiguracao</c> fabricada é append-only por gatilho e não pode ser
/// removida, então uma referência fabricada aqui — a outra convenção, de
/// propósito — bloquearia a precondição provada lá se as duas classes
/// dividissem o banco.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste, sem valor externo interpolado.")]
public sealed class AlgoritmoContagemAvancaDataUtilPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public AlgoritmoContagemAvancaDataUtilPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private const string ArquivoDaMigration = "20260813202454_AddAlgoritmoContagemAvancaDataUtil.cs";

    private const string PredicadoDaEntrada =
        """@.codigo == "CONTAGEM-PRAZO-AVANCA-DATA-UTIL" && @.versao == "v1" && exists(@.hash)""";

    /// <summary>
    /// O mesmo bloco do <c>Down</c> da migration: nomeia só a entrada que ela
    /// remove.
    /// </summary>
    private const string PrecondicaoDaMigration = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "CONTAGEM-PRAZO-AVANCA-DATA-UTIL" && @.versao == "v1" && exists(@.hash))'
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: entrada de algoritmo de contagem referenciada por versão de configuração congelada; remover viola o append-only (ADR-0112)';
            END IF;
        END
        $adr0112$;
        """;

    [Fact(DisplayName = "A precondição olha só a própria entrada e aborta apenas diante da referência real")]
    public async Task Precondicao_EscopadaNaPropriaEntrada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Task ReverterAsync() => FronteiraAppendOnlyDoRol.ExecutarAsync(context, PrecondicaoDaMigration);

        // Sem referência alguma, reverter é legítimo: a entrada ainda é
        // vocabulário, não fato (ADR-0112).
        await ReverterAsync();

        // Referência congelada a OUTRA convenção de contagem não bloqueia esta
        // reversão — é o controle que pega um predicado ampliado por engano.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outra-convencao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora, "v1"));
        await ReverterAsync();

        // Nem referência a uma versão que este Down não remove.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outra-versao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v2"));
        await ReverterAsync();

        // Nem um homônimo: objeto com a chave bare `codigo` de mesmo valor, sem
        // a tripla, é outra coisa — uma fase, por exemplo.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-fase-homonima",
            FronteiraAppendOnlyDoRol.FaseHomonima(AlgoritmoContagemPrazoCodigo.AvancaDataUtil));
        await ReverterAsync();

        // A referência real, sim: a partir dela a entrada é fato, e a
        // precondição aborta antes de alterar qualquer linha.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "referencia-real",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v1"));

        Func<Task> reversao = ReverterAsync;

        (await reversao.Should().ThrowAsync<DbException>(
            "a precondição da migration aborta diante da referência congelada"))
            .WithMessage("*ADR-0112*");
    }

    [Fact(DisplayName = "O Down da migration carrega a precondição — o SQL provado aqui é o executado lá")]
    public void MigrationDown_CarregaAPrecondicao()
    {
        string guarda = FronteiraAppendOnlyDoRol.BlocoDown(
            FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration, Origem()));

        foreach (string marca in new[] { "$adr0112$", "RAISE EXCEPTION", PredicadoDaEntrada })
        {
            guarda.Should().Contain(
                marca,
                "sem a precondição no Down, a prova comportamental desta classe deixaria de refletir a migration real");
        }

        // A guarda nomeia uma entrada só: ampliá-la para as outras convenções
        // faria esta reversão falhar por referência a entrada que ela não remove.
        guarda.Should().NotContain(
            AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial,
            "a guarda não pode olhar a convenção que exclui o dia inicial, que esta migration não remove");
        guarda.Should().NotContain(
            AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora,
            "a guarda não pode olhar a convenção por horas úteis, que esta migration não remove");
    }

    private static string Origem([CallerFilePath] string origem = "") => origem;
}
