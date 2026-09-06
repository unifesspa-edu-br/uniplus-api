namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova executável da precondição de migration da fronteira append-only do
/// <c>rol_de_regras</c> (ADR-0112) para a issue #1408: a <c>v1</c> de
/// <c>DISTRIB-VAGAS-LEI-12711</c> e <c>DISTRIB-VAGAS-INSTITUCIONAL</c> só é
/// retirada do catálogo se nenhuma configuração — congelada OU rascunho vivo
/// — a referenciar. A <c>v2</c> (SeedId 23/24, PR #1389) é superset e cobre o
/// que a v1 cobria. O SQL exercitado é o mesmo do <c>Up</c> da migration
/// <c>RetiraRegraDistribuicaoVagasV1DuplicadaDaV2</c> — a guarda fica no
/// <c>Up</c>, não no <c>Down</c>, porque é o <c>Up</c> que remove as linhas
/// aqui (o <c>Down</c> só reinsere).
/// </summary>
/// <remarks>
/// O cenário de RASCUNHO VIVO (<see cref="RetiraRegraDistribuicaoVagasV1RascunhoVivoPrecondicaoTests"/>)
/// mora em classe própria, com fixture própria: um congelamento fabricado
/// aqui é forense, permanente por gatilho, e permaneceria no banco
/// compartilhado da classe para sempre — poluindo a asserção "sem referência
/// alguma, a precondição passa" que o outro cenário precisa no início.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste, sem valor externo interpolado.")]
public sealed class RetiraRegraDistribuicaoVagasV1PrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RetiraRegraDistribuicaoVagasV1PrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// O bloco COMPLETO do <c>Up</c> da migration
    /// <c>RetiraRegraDistribuicaoVagasV1DuplicadaDaV2</c> — cobre tanto o
    /// snapshot congelado (<c>versoes_configuracao</c>) quanto o rascunho vivo
    /// (<c>configuracoes_distribuicao_vagas</c>). <see cref="MigrationUp_CarregaAPrecondicao"/>
    /// prova que este texto é o que a migration real carrega; o cenário
    /// comportamental desta classe usa <see cref="PrecondicaoSoCongelada"/>
    /// (metade do bloco) — ver o porquê no remark da classe.
    /// </summary>
    internal const string PrecondicaoDaMigration = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-LEI-12711" && @.versao == "v1" && exists(@.hash))'
                   OR configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-INSTITUCIONAL" && @.versao == "v1" && exists(@.hash))'
            ) OR EXISTS (
                SELECT 1
                FROM selecao.configuracoes_distribuicao_vagas
                WHERE (regra_distribuicao_codigo = 'DISTRIB-VAGAS-LEI-12711' AND regra_distribuicao_versao = 'v1')
                   OR (regra_distribuicao_codigo = 'DISTRIB-VAGAS-INSTITUCIONAL' AND regra_distribuicao_versao = 'v1')
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: v1 de DISTRIB-VAGAS-LEI-12711/INSTITUCIONAL referenciada por versão de configuração congelada ou por rascunho vivo; remover viola o append-only (ADR-0112)';
            END IF;
        END
        $adr0112$;
        """;

    /// <summary>
    /// Só a metade do bloco acima que olha <c>versoes_configuracao</c> — usada
    /// pelo cenário desta classe porque <see cref="FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync"/>
    /// chama <c>ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme</c>, que
    /// grava (efeito colateral pré-existente, alheio a este cenário) um
    /// rascunho vivo em <c>DISTRIB-VAGAS-INSTITUCIONAL v1</c> a cada chamada.
    /// Rodar o bloco COMBINADO aqui trip-aria a guarda no primeiro cenário —
    /// falso positivo de poluição de fixture, não achado sobre a metade
    /// congelada que este teste prova. O rascunho vivo tem cenário e classe
    /// próprios (<see cref="RetiraRegraDistribuicaoVagasV1RascunhoVivoPrecondicaoTests"/>).
    /// </summary>
    private const string PrecondicaoSoCongelada = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-LEI-12711" && @.versao == "v1" && exists(@.hash))'
                   OR configuracao_congelada @? '$.** ? (@.codigo == "DISTRIB-VAGAS-INSTITUCIONAL" && @.versao == "v1" && exists(@.hash))'
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: v1 de DISTRIB-VAGAS-LEI-12711/INSTITUCIONAL referenciada por versão de configuração congelada; remover viola o append-only (ADR-0112)';
            END IF;
        END
        $adr0112$;
        """;

    [Fact(DisplayName = "A precondição aborta diante de referência CONGELADA — e só diante dela")]
    public async Task Precondicao_AbortaDianteDeReferenciaCongelada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Sem referência alguma, a precondição passa em silêncio: retirar a v1 é
        // legítimo num banco onde nenhuma configuração a congelou — ela ainda é
        // vocabulário de seed (ADR-0112).
        await ExecutarPrecondicaoAsync(context);

        // Identidade, não substring: um código diferente que contém o procurado
        // como prefixo não é o procurado.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "prefixo",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia("DISTRIB-VAGAS-INSTITUCIONAL-LEGADO", "v1"));
        await ExecutarPrecondicaoAsync(context);

        // A busca é por referência de regra — a tripla {codigo, versao, hash} —,
        // não por ocorrência do texto: uma fase batizada com o código de uma
        // regra de distribuição não é uma referência a ela.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "fase-homonima",
            FronteiraAppendOnlyDoRol.FaseHomonima("DISTRIB-VAGAS-LEI-12711"));
        await ExecutarPrecondicaoAsync(context);

        // Referência à v2: a v1 continua removível enquanto ninguém a referenciar
        // — uma configuração sob v2 não impede retirar a v1 duplicada.
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "outra-versao",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia("DISTRIB-VAGAS-LEI-12711", "v2"));
        await ExecutarPrecondicaoAsync(context);

        // Referência real fabricada para DISTRIB-VAGAS-INSTITUCIONAL v1: a
        // partir da primeira configuração congelada que a referencia, ela é
        // fato — a precondição encontra a referência e aborta antes de
        // alterar qualquer linha. (DISTRIB-VAGAS-LEI-12711 é o alvo do
        // cenário de rascunho vivo, na classe irmã — mesmo predicado, mesma
        // estrutura de guarda, código diferente.)
        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "referencia-real",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia("DISTRIB-VAGAS-INSTITUCIONAL", "v1"));

        Func<Task> remocao = () => ExecutarPrecondicaoAsync(context);

        (await remocao.Should().ThrowAsync<DbException>(
            "a precondição da migration aborta diante da referência congelada"))
            .WithMessage("*ADR-0112*");
    }

    [Fact(DisplayName = "O Up da migration carrega a precondição — o SQL provado aqui é o executado lá")]
    public void MigrationUp_CarregaAPrecondicao()
    {
        string guarda = FronteiraAppendOnlyDoRol.BlocoUp(
            FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration));

        foreach (string marca in new[]
        {
            "$adr0112$",
            "RAISE EXCEPTION",
            """@.codigo == "DISTRIB-VAGAS-LEI-12711" && @.versao == "v1" && exists(@.hash)""",
            """@.codigo == "DISTRIB-VAGAS-INSTITUCIONAL" && @.versao == "v1" && exists(@.hash)""",
            "FROM selecao.configuracoes_distribuicao_vagas",
            "regra_distribuicao_codigo = 'DISTRIB-VAGAS-LEI-12711' AND regra_distribuicao_versao = 'v1'",
            "regra_distribuicao_codigo = 'DISTRIB-VAGAS-INSTITUCIONAL' AND regra_distribuicao_versao = 'v1'",
        })
        {
            guarda.Should().Contain(
                marca,
                "sem a precondição no Up, a prova comportamental desta classe deixaria de refletir a migration real");
        }

        // A guarda protege só a v1 — as regras nomeadas que nasceram no mesmo PR
        // #1389 (PSIQ e Edu. Campo) não são tocadas por esta migration.
        guarda.Should().NotContain("DISTRIB-VAGAS-PSIQ");
        guarda.Should().NotContain("DISTRIB-VAGAS-EDU-CAMPO");
    }

    internal const string ArquivoDaMigration = "20260903031131_RetiraRegraDistribuicaoVagasV1DuplicadaDaV2.cs";

    private static Task ExecutarPrecondicaoAsync(SelecaoDbContext context) =>
        FronteiraAppendOnlyDoRol.ExecutarAsync(context, PrecondicaoSoCongelada);
}
