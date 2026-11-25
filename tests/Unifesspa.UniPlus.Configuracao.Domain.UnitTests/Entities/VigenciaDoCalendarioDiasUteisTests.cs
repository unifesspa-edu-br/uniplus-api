namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.DiaNaoUtilCriacaoDeTeste;

public sealed class VigenciaDoCalendarioDiasUteisTests
{
    [Fact(DisplayName = "MarcarVigente em dataset recém-criado tem sucesso")]
    public void MarcarVigente_DatasetRecemCriado_Sucesso()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result resultado = calendario.MarcarVigente();

        resultado.IsSuccess.Should().BeTrue();
        calendario.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "MarcarVigente chamado duas vezes falha na segunda chamada (JaVigente)")]
    public void MarcarVigente_ChamadoDuasVezes_FalhaNaSegunda()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        calendario.MarcarVigente();

        Result resultado = calendario.MarcarVigente();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.JaVigente);
        calendario.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "MarcarNaoVigente volta Vigente para false")]
    public void MarcarNaoVigente_VoltaParaFalse()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        calendario.MarcarVigente();

        calendario.MarcarNaoVigente();

        calendario.Vigente.Should().BeFalse();
    }
}
