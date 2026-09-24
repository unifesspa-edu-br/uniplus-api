namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova executável da precondição da migration que semeia a eliminação por falta em dia de
/// prova do ENEM: o <c>Down</c> só remove a entrada enquanto nenhuma configuração congelada a
/// referenciar (ADR-0112), e olha só a própria entrada.
/// </summary>
/// <remarks>
/// Fixture próprio: a <c>VersaoConfiguracao</c> fabricada é append-only por gatilho e não pode
/// ser removida, então uma referência fabricada aqui bloquearia a precondição provada por outra
/// classe se as duas dividissem o banco.
/// </remarks>
public sealed class EliminacaoPorFaltaEmDiaDeProvaEnemPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public EliminacaoPorFaltaEmDiaDeProvaEnemPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private const string ArquivoDaMigration = "20260924092802_SemeiaEliminacaoPorFaltaEmDiaDeProvaEnem.cs";

    private const string PredicadoDaEntrada =
        """@.codigo == "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM" && @.versao == "v1" && exists(@.hash)""";

    /// <summary>O mesmo bloco do <c>Down</c> da migration: nomeia só a entrada que ela remove.</summary>
    internal const string PrecondicaoDaMigration = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM" && @.versao == "v1" && exists(@.hash))'
            ) OR EXISTS (
                SELECT 1
                FROM selecao.regras_eliminacao
                WHERE regra_codigo = 'ELIM-FALTA-EM-DIA-DE-PROVA-ENEM' AND regra_versao = 'v1'
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: eliminação por falta em dia de prova do ENEM referenciada por versão de configuração congelada ou por rascunho vivo; remover viola o append-only (ADR-0112)';
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

        // Referência congelada a OUTRA regra de eliminação não bloqueia esta reversão.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outra-eliminacao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(RegraEliminacaoCodigo.ElimZeroEmArea, "v1"));
        await ReverterAsync();

        // Nem referência a uma versão que este Down não remove.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-outra-versao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v2"));
        await ReverterAsync();

        // Nem um homônimo: objeto com a chave bare `codigo` de mesmo valor, sem a tripla.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "escopo-fase-homonima",
            FronteiraAppendOnlyDoRol.FaseHomonima(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem));
        await ReverterAsync();

        // A referência real, sim: a partir dela a entrada é fato, e a precondição aborta antes
        // de alterar qualquer linha.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "referencia-real",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1"));

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

        foreach (string marca in new[]
        {
            "$adr0112$",
            "RAISE EXCEPTION",
            PredicadoDaEntrada,
            "FROM selecao.regras_eliminacao",
            "regra_codigo = 'ELIM-FALTA-EM-DIA-DE-PROVA-ENEM' AND regra_versao = 'v1'",
        })
        {
            guarda.Should().Contain(
                marca,
                "sem a precondição no Down, a prova comportamental desta classe deixaria de refletir a migration real");
        }

        // O Down também restaura o comentário de baseado_em_enem, que cita as outras eliminações
        // do ENEM: o que não pode citá-las é o predicado da guarda.
        guarda.Should().NotContain(
            $"@.codigo == \"{RegraEliminacaoCodigo.ElimZeroEmArea}\"",
            "a guarda não pode olhar outra regra de eliminação, que esta migration não remove");
    }
}
