namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class GrupoCursoTests
{
    [Theory(DisplayName = "Criar aceita os quatro grupos canônicos da Res. 805")]
    [InlineData(GrupoCurso.Tecnologica)]
    [InlineData(GrupoCurso.HumanisticaI)]
    [InlineData(GrupoCurso.HumanisticaII)]
    [InlineData(GrupoCurso.SaudeEBiologicas)]
    public void Criar_GrupoCanonico_Aceita(string valor)
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Fact(DisplayName = "Criar normaliza espaços nas bordas (Trim)")]
    public void Criar_ComEspacos_Normaliza()
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar("  Tecnológica  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(GrupoCurso.Tecnologica);
    }

    [Theory(DisplayName = "Criar rejeita grupo fora do domínio")]
    [InlineData("Engenharias")]
    [InlineData("Linguística")]
    [InlineData("Humanística III")]
    [InlineData("tecnológica")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_GrupoForaDoDominio_Falha(string valor)
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
    }

    [Theory(DisplayName = "EhValido reflete a pertinência ao domínio fechado")]
    [InlineData(GrupoCurso.Tecnologica, true)]
    [InlineData(GrupoCurso.SaudeEBiologicas, true)]
    [InlineData("Engenharias", false)]
    [InlineData("", false)]
    public void EhValido_ReflectePertinencia(string valor, bool esperado)
    {
        GrupoCurso.EhValido(valor).Should().Be(esperado);
    }
}
