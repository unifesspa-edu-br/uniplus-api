namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma que a regra de cada ação afirmativa precisa ter: a que se soma às cotas da lei não cita a
/// opção pelas cotas, e as demais a recusam em toda cláusula (UNI-REQ-0142 e UNI-REQ-0266).
/// </summary>
public sealed class OpcaoPelasCotasDaLeiTests
{
    private const string AcaoAfirmativaInstitucional = "AC_I";
    private const string FatoDeficiencia = "PCD";
    private const string FatoConcorrerComoDeficiente = "CONCORRER_PCD";

    [Fact(DisplayName = "A reserva para pessoa com deficiência, com os fatos de deficiência e sem citar a opção, tem a forma da ação afirmativa")]
    public void RegraTemAFormaDaAcaoAfirmativa_ReservaParaPessoaComDeficienciaSemCitarAOpcao_Tem()
    {
        PredicadoDnf regra = Regra(Condicao(FatoDeficiencia, true), Condicao(FatoConcorrerComoDeficiente, true));

        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas, regra)
            .Should().BeTrue();
    }

    [Fact(DisplayName = "A reserva para pessoa com deficiência que exige não ter optado pelas cotas não tem a forma da ação afirmativa")]
    public void RegraTemAFormaDaAcaoAfirmativa_ReservaParaPessoaComDeficienciaQueExigeNaoTerOptado_NaoTem()
    {
        PredicadoDnf regra = Regra(Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao(FatoDeficiencia, true));

        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas, regra)
            .Should().BeFalse();
    }

    [Fact(DisplayName = "A reserva para pessoa com deficiência que exige ter optado pelas cotas não tem a forma da ação afirmativa")]
    public void RegraTemAFormaDaAcaoAfirmativa_ReservaParaPessoaComDeficienciaQueExigeTerOptado_NaoTem()
    {
        PredicadoDnf regra = Regra(Condicao(OpcaoPelasCotasDaLei.Fato, true), Condicao(FatoDeficiencia, true));

        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas, regra)
            .Should().BeFalse();
    }

    [Fact(DisplayName = "A reserva para pessoa com deficiência sem nenhuma condição não tem a forma da ação afirmativa")]
    public void RegraTemAFormaDaAcaoAfirmativa_ReservaParaPessoaComDeficienciaSemCondicao_NaoTem()
    {
        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(OpcaoPelasCotasDaLei.AcaoAfirmativaQueSeSomaAsCotas, PredicadoDnf.SemClausula)
            .Should().BeFalse();
    }

    [Fact(DisplayName = "A ação afirmativa que não se soma às cotas tem a forma só quando recusa a opção pelas cotas")]
    public void RegraTemAFormaDaAcaoAfirmativa_AcaoAfirmativaInstitucional_ExigeNaoTerOptado()
    {
        PredicadoDnf recusaAOpcao = Regra(Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao(FatoDeficiencia, true));
        PredicadoDnf naoCitaAOpcao = Regra(Condicao(FatoDeficiencia, true));

        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(AcaoAfirmativaInstitucional, recusaAOpcao).Should().BeTrue();
        OpcaoPelasCotasDaLei.RegraTemAFormaDaAcaoAfirmativa(AcaoAfirmativaInstitucional, naoCitaAOpcao).Should().BeFalse();
    }

    [Theory(DisplayName = "Só a reserva para pessoa com deficiência se soma às cotas, e o código é comparado letra a letra")]
    [InlineData("AC_PCD", true)]
    [InlineData("AC_I", false)]
    [InlineData("AC_Q", false)]
    [InlineData("ac_pcd", false)]
    public void SeSomaAsCotas_ComparaOCodigoExato(string codigo, bool seSoma) =>
        OpcaoPelasCotasDaLei.SeSomaAsCotas(codigo).Should().Be(seSoma);

    private static PredicadoDnf Regra(params CondicaoDnf[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, c))]).Value!;

    private static CondicaoDnf Condicao(string fato, bool valor) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;
}
