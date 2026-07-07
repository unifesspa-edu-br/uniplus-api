namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConfiguracaoBonusRegionalTests
{
    private static ReferenciaRegra RegraMultiplicativo() =>
        ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!;

    [Fact(DisplayName = "Criar com fator válido e sem teto tem sucesso (P.O.: ×1,20 sem teto)")]
    public void Criar_SemTeto_Sucesso()
    {
        Result<ConfiguracaoBonusRegional> resultado = ConfiguracaoBonusRegional.Criar(
            RegraMultiplicativo(), 1.20m, null, "Marabá", "RN05");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Teto.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com teto informado tem sucesso")]
    public void Criar_ComTeto_Sucesso()
    {
        Result<ConfiguracaoBonusRegional> resultado = ConfiguracaoBonusRegional.Criar(
            RegraMultiplicativo(), 1.20m, 10m, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Teto.Should().Be(10m);
    }

    [Fact(DisplayName = "Criar com regra de código diferente de BONUS-MULTIPLICATIVO falha")]
    public void Criar_RegraInvalida_Falha()
    {
        ReferenciaRegra regraErrada = ReferenciaRegra.Criar("FORMULA-MEDIA-PONDERADA", "v1", new string('b', 64)).Value!;

        Result<ConfiguracaoBonusRegional> resultado = ConfiguracaoBonusRegional.Criar(regraErrada, 1.20m, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraInvalida");
    }

    [Theory(DisplayName = "Criar com fator não positivo falha")]
    [InlineData(0)]
    [InlineData(-1.2)]
    public void Criar_FatorInvalido_Falha(double fator)
    {
        Result<ConfiguracaoBonusRegional> resultado = ConfiguracaoBonusRegional.Criar(
            RegraMultiplicativo(), (decimal)fator, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoBonusRegional.FatorInvalido");
    }

    [Fact(DisplayName = "Criar com teto não positivo falha")]
    public void Criar_TetoInvalido_Falha()
    {
        Result<ConfiguracaoBonusRegional> resultado = ConfiguracaoBonusRegional.Criar(
            RegraMultiplicativo(), 1.20m, 0m, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoBonusRegional.TetoInvalido");
    }
}
