namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

/// <summary>
/// As guardas da migration que corrige a <c>BaseLegal</c> de
/// <c>REMANEJ-CASCATA-LEI-12711/v1</c> no lugar (issue #1454) — mesmo padrão de
/// <c>EsquemaArgsDaRegraDeRecursoSemAtoAncoraCodigo</c>: corrigir a definição no lugar só é
/// legítimo enquanto a entrada for vocabulário, não fato (ADR-0112).
/// </summary>
/// <remarks>
/// <para>
/// Duas populações referenciam a regra, e cada uma pede um tratamento. A <b>congelada</b> —
/// <c>versoes_configuracao</c> — é imutável, então a migration aborta diante dela. A
/// <b>viva</b> — <c>configuracoes_cascata_remanejamento</c> de rascunho — guarda o hash da
/// regra referenciada em coluna própria (<c>regra_hash</c>) e não é revalidada contra o
/// catálogo ao ser reidratada pelo EF; sem reapontar, o rascunho ficaria com um hash que não
/// descreve mais definição nenhuma do catálogo.
/// </para>
/// <para>
/// A validade sintática do SQL não é objeto deste arquivo: a suíte de integração aplica todas
/// as migrations contra um Postgres real ao subir, e um bloco malformado derrubaria o fixture
/// antes de qualquer asserção aqui.
/// </para>
/// </remarks>
public sealed class CorrigeBaseLegalCascataLei12711GuardaTests
{
    private const string ArquivoDaMigration = "20260908153633_CorrigeBaseLegalCascataLei12711.cs";

    private static string Migration() => FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration);

    [Fact(DisplayName = "A migration aborta diante de configuração congelada que referencia a entrada")]
    public void Up_GuardaAConfiguracaoCongelada()
    {
        string migration = Migration();

        migration.Should().Contain("selecao.versoes_configuracao");
        migration.Should().Contain(
            """@.codigo == "REMANEJ-CASCATA-LEI-12711" && @.versao == "v1" && exists(@.hash)""",
            "a referência é a tripla — procurar só pela chave bare 'codigo' pegaria homônimo, que não é referência");
        migration.Should().Contain("ADR-0112");
    }

    [Fact(DisplayName = "A migration reaponta o hash das cascatas vivas que ainda referenciam a definição")]
    public void Up_ReapontaOHashDasCascatasVivas()
    {
        string migration = Migration();

        migration.Should().Contain("UPDATE selecao.configuracoes_cascata_remanejamento",
            "sem versão sucessora, o hash antigo deixaria de descrever definição alguma do catálogo");
        migration.Should().Contain("SET regra_hash");
        migration.Should().Contain("""regra_codigo = 'REMANEJ-CASCATA-LEI-12711'""");
    }

    [Fact(DisplayName = "A reversão devolve o hash anterior e responde à mesma fronteira do avanço")]
    public void Down_DevolveOHashAnteriorEGuardaDeNovo()
    {
        string down = FronteiraAppendOnlyDoRol.BlocoDown(Migration());

        down.Should().Contain("ReapontarHashDasCascatasVivas",
            "voltar a definição sem voltar a referência viva deixaria o rascunho apontando para um hash que não existe mais");
        down.Should().Contain("ExigirQueNenhumaConfiguracaoCongeladaReferencie",
            "a reversão responde à mesma fronteira do avanço");
        down.Should().Contain("HashDaDefinicaoAnterior");
    }
}
