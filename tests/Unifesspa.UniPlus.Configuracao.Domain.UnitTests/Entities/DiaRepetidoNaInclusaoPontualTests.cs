namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.DiaNaoUtilCriacaoDeTeste;

public sealed class DiaRepetidoNaInclusaoPontualTests
{
    private static readonly DateOnly Data = new(2027, 1, 1);

    private static DiaNaoUtilCriacao Estadual(string uf) => new("ESTADUAL", null, null, null, Data, "Data magna do estado", uf);

    private static CalendarioDiasUteis CalendarioCom(params DiaNaoUtilCriacao[] dias) =>
        CalendarioDiasUteis.Criar("2027.1", dias).ValorEsperado();

    [Fact(DisplayName = "Dia incluído pontualmente entra no controle de repetição das inclusões seguintes")]
    public void IncluirDiaNaoUtil_MesmoDiaDuasVezes_RecusaASegunda()
    {
        CalendarioDiasUteis calendario = CalendarioCom(Nacional());
        calendario.IncluirDiaNaoUtil(Estadual("PA")).IsSuccess.Should().BeTrue();

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(Estadual("PA"));

        resultado.Error?.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
        calendario.DiasNaoUteis.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Mesma data e abrangência com UF diferente não é repetição na inclusão pontual")]
    public void IncluirDiaNaoUtil_UfDiferente_Aceita()
    {
        CalendarioDiasUteis calendario = CalendarioCom(Estadual("PA"));

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(Estadual("AP"));

        resultado.IsSuccess.Should().BeTrue();
        calendario.DiasNaoUteis.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Dia recusado por campo inválido não entra no controle de repetição")]
    public void IncluirDiaNaoUtil_RecusadoPorCampo_NaoImpedeOValido()
    {
        CalendarioDiasUteis calendario = CalendarioCom(Estadual("PA"));
        calendario.IncluirDiaNaoUtil(new DiaNaoUtilCriacao("NACIONAL", null, null, null, Data, " ")).IsFailure.Should().BeTrue();

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(Nacional());

        resultado.IsSuccess.Should().BeTrue();
    }
}
