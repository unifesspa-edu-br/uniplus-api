namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Partição fechada de <see cref="RegraDistribuicaoVagasCodigo"/>: todo código reconhecido é
/// ramo federal OU quadro fixo, nunca os dois nem nenhum. É o que impede a próxima regra de
/// distribuição de cair no ramo "nem federal nem quadro fixo" — quadro vazio materializado sem
/// erro (o mesmo buraco que deixou PSIQ e a antiga EDU-CAMPO irreidratáveis antes de existir
/// <see cref="RegraDistribuicaoVagasCodigo.EhQuadroFixo"/>, e que deixaria uma regra federal
/// nova no mesmo buraco do lado oposto).
/// </summary>
public sealed class RegraDistribuicaoVagasCodigoTests
{
    [Fact(DisplayName = "Todo código de RegraDistribuicaoVagasCodigo.Todos é ramo federal OU quadro fixo, nunca os dois")]
    public void EhRamoFederal_EhQuadroFixo_ParticaoFechada()
    {
        foreach (string codigo in RegraDistribuicaoVagasCodigo.Todos)
        {
            bool ehRamoFederal = RegraDistribuicaoVagasCodigo.EhRamoFederal(codigo);
            bool ehQuadroFixo = RegraDistribuicaoVagasCodigo.EhQuadroFixo(codigo);

            (ehRamoFederal ^ ehQuadroFixo).Should().BeTrue(
                $"'{codigo}' precisa cair em exatamente um dos dois ramos — nos dois materializaria quadro " +
                "duas vezes, em nenhum materializaria quadro vazio sem erro.");
        }
    }

    [Fact(DisplayName = "Todo código do seed de RegraDistribuicaoVagas está em RegraDistribuicaoVagasCodigo.Todos")]
    public void Todos_CobreOsCincoCodigosCanonicos()
    {
        RegraDistribuicaoVagasCodigo.Todos.Should().BeEquivalentTo(
        [
            RegraDistribuicaoVagasCodigo.Lei12711,
            RegraDistribuicaoVagasCodigo.Lei12711ComAcPcd,
            RegraDistribuicaoVagasCodigo.Institucional,
            RegraDistribuicaoVagasCodigo.Psiq,
            RegraDistribuicaoVagasCodigo.ComPcdPuro,
        ]);
    }

    [Fact(DisplayName = "RolFechado é null só para o código de rol aberto (Institucional)")]
    public void RolFechado_NullApenasParaInstitucional()
    {
        foreach (string codigo in RegraDistribuicaoVagasCodigo.Todos)
        {
            IReadOnlyList<string>? rol = RegraDistribuicaoVagasCodigo.RolFechado(codigo);

            if (codigo == RegraDistribuicaoVagasCodigo.Institucional)
            {
                rol.Should().BeNull("Institucional é o único código de rol aberto");
            }
            else
            {
                rol.Should().NotBeNullOrEmpty($"'{codigo}' é um código de quadro fechado");
            }
        }
    }

    [Fact(DisplayName = "RolFechado da variação -COM-AC-PCD é o rol da Lei 12.711 pura mais AC_PCD")]
    public void RolFechado_Lei12711ComAcPcd_EhOSupersetDaLei12711Pura()
    {
        IReadOnlyList<string> rolPuro = RegraDistribuicaoVagasCodigo.RolFechado(RegraDistribuicaoVagasCodigo.Lei12711)!;
        IReadOnlyList<string> rolComAcPcd = RegraDistribuicaoVagasCodigo.RolFechado(RegraDistribuicaoVagasCodigo.Lei12711ComAcPcd)!;

        rolComAcPcd.Should().BeEquivalentTo([.. rolPuro, "AC_PCD"]);
    }
}
