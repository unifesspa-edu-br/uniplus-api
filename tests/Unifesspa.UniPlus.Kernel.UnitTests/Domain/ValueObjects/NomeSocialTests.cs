namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class NomeSocialTests
{
    [Fact]
    public void Criar_DadoNomeCivilValido_DeveRetornarSucesso()
    {
        Result<NomeSocial> resultado = NomeSocial.Criar("Maria Silva");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.NomeCivil.Should().Be("Maria Silva");
        resultado.Value.Nome.Should().BeNull();
    }

    [Fact]
    public void Criar_DadoNomeCivilNulo_DeveRetornarFailure()
    {
        Result<NomeSocial> resultado = NomeSocial.Criar(null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NomeSocial.NomeCivilVazio");
    }

    [Fact]
    public void Criar_DadoNomeCivilVazio_DeveRetornarFailure()
    {
        Result<NomeSocial> resultado = NomeSocial.Criar(string.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NomeSocial.NomeCivilVazio");
    }

    [Fact]
    public void NomeExibicao_SemNomeSocial_DeveRetornarNomeCivil()
    {
        NomeSocial nome = NomeSocial.Criar("Maria Silva").Value!;

        nome.NomeExibicao.Should().Be("Maria Silva");
        nome.UsaNomeSocial.Should().BeFalse();
    }

    [Fact]
    public void NomeExibicao_ComNomeSocial_DeveRetornarNomeSocial()
    {
        NomeSocial nome = NomeSocial.Criar("Maria Silva", "Mara").Value!;

        nome.NomeExibicao.Should().Be("Mara");
        nome.UsaNomeSocial.Should().BeTrue();
        nome.Nome.Should().Be("Mara");
    }

    [Fact]
    public void Criar_DeveAplicarTrimNoNomeCivil()
    {
        NomeSocial nome = NomeSocial.Criar("  Maria Silva  ").Value!;

        nome.NomeCivil.Should().Be("Maria Silva");
    }

    [Fact]
    public void Criar_DeveAplicarTrimNoNomeSocial()
    {
        NomeSocial nome = NomeSocial.Criar("Maria Silva", "  Mara  ").Value!;

        nome.Nome.Should().Be("Mara");
    }

    [Fact]
    public void ToString_SemNomeSocial_DeveRetornarNomeCivil()
    {
        NomeSocial nome = NomeSocial.Criar("Maria Silva").Value!;

        nome.ToString().Should().Be("Maria Silva");
    }

    [Fact]
    public void ToString_ComNomeSocial_DeveRetornarNomeSocial()
    {
        NomeSocial nome = NomeSocial.Criar("Maria Silva", "Mara").Value!;

        nome.ToString().Should().Be("Mara");
    }
}
