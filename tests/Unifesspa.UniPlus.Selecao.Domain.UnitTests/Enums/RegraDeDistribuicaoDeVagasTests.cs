namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Partição fechada de <see cref="RegraDeDistribuicaoDeVagas"/>: todo código reconhecido é
/// ramo federal OU quadro fixo, nunca os dois nem nenhum. É o que impede a próxima regra de
/// distribuição de cair no ramo "nem federal nem quadro fixo" — quadro vazio materializado sem
/// erro (o mesmo buraco que deixou PSIQ e a antiga EDU-CAMPO irreidratáveis antes de existir
/// <see cref="PoliticaDaDistribuicaoDeVagas.EhQuadroFixo"/>, e que deixaria uma regra federal
/// nova no mesmo buraco do lado oposto).
/// </summary>
public sealed class RegraDeDistribuicaoDeVagasTests
{
    [Fact(DisplayName = "Toda regra de distribuição usa a Lei de Cotas ou quadro fixado no edital")]
    public void EhRegraFederal_EhQuadroFixo_ParticaoFechada()
    {
        foreach (string codigo in CatalogoDe<RegraDeDistribuicaoDeVagas>.Todos().Select(static item => item.Codigo))
        {
            bool ehRegraFederal = PoliticaDaDistribuicaoDeVagas.EhRegraFederal(codigo);
            bool ehQuadroFixo = PoliticaDaDistribuicaoDeVagas.EhQuadroFixo(codigo);

            (ehRegraFederal ^ ehQuadroFixo).Should().BeTrue(
                $"'{codigo}' precisa cair em exatamente um dos dois ramos — nos dois materializaria quadro " +
                "duas vezes, em nenhum materializaria quadro vazio sem erro.");
        }
    }

    [Fact(DisplayName = "O catálogo de distribuição contém os cinco códigos conhecidos")]
    public void Todos_CobreOsCincoCodigosCanonicos()
    {
        CatalogoDe<RegraDeDistribuicaoDeVagas>.Todos().Select(static item => item.Codigo).Should().BeEquivalentTo(
        [
            RegraDeDistribuicaoDeVagas.Lei12711.Codigo(),
            RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo(),
            RegraDeDistribuicaoDeVagas.Institucional.Codigo(),
            RegraDeDistribuicaoDeVagas.PorAcrescimoIndigenaEQuilombola.Codigo(),
            RegraDeDistribuicaoDeVagas.ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo(),
        ]);
    }

    [Fact(DisplayName = "RolFechado é null só para o código de rol aberto (Institucional)")]
    public void RolFechado_NullApenasParaInstitucional()
    {
        foreach (string codigo in CatalogoDe<RegraDeDistribuicaoDeVagas>.Todos().Select(static item => item.Codigo))
        {
            IReadOnlyList<string>? rol = PoliticaDaDistribuicaoDeVagas.RolFechado(codigo);

            if (codigo == RegraDeDistribuicaoDeVagas.Institucional.Codigo())
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
        IReadOnlyList<string> rolPuro = PoliticaDaDistribuicaoDeVagas.RolFechado(RegraDeDistribuicaoDeVagas.Lei12711.Codigo())!;
        IReadOnlyList<string> rolComAcPcd = PoliticaDaDistribuicaoDeVagas.RolFechado(RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo())!;

        rolComAcPcd.Should().BeEquivalentTo([.. rolPuro, "AC_PCD"]);
    }

    [Fact(DisplayName = "RamoDe diz o ramo de cada código reconhecido, e nenhum fica sem ramo")]
    public void RamoDe_CadaCodigoReconhecidoTemUmRamo()
    {
        foreach (string codigo in CatalogoDe<RegraDeDistribuicaoDeVagas>.Todos().Select(static item => item.Codigo))
        {
            string esperado = PoliticaDaDistribuicaoDeVagas.EhRegraFederal(codigo)
                ? ModoDeMontagemDoQuadroDeVagas.LeiDeCotas.Codigo()
                : ModoDeMontagemDoQuadroDeVagas.QuadroFixadoNoEdital.Codigo();

            PoliticaDaDistribuicaoDeVagas.ModoDeMontagemDe(codigo).Should().Be(esperado, $"'{codigo}' pertence a um dos dois ramos");
        }
    }

    [Fact(DisplayName = "RamoDe é nulo para um código que não é de regra de distribuição")]
    public void RamoDe_CodigoDesconhecido_Nulo() =>
        PoliticaDaDistribuicaoDeVagas.ModoDeMontagemDe("DESEMPATE-QUALQUER").Should().BeNull();
}
