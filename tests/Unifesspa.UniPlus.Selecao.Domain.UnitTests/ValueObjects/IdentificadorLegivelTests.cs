namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class IdentificadorLegivelTests
{
    [Theory(DisplayName = "Identificador em kebab-case é aceito")]
    [InlineData("psiq-2026")]
    [InlineData("medicina-2027")]
    [InlineData("abc")]
    [InlineData("a1b")]
    [InlineData("ppg-ciencias-florestais-2026")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<IdentificadorLegivel> resultado = IdentificadorLegivel.Criar(valor);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value.Valor.Should().Be(valor);
    }

    [Fact(DisplayName = "Espaços nas pontas são descartados antes de validar")]
    public void Criar_ComEspacosNasPontas_Normaliza()
    {
        IdentificadorLegivel.Criar("  psiq-2026 ").Value.Valor.Should().Be("psiq-2026");
    }

    [Theory(DisplayName = "Formato fora do kebab-case é recusado com erro de formato")]
    [InlineData("PSIQ-2026")]
    [InlineData("psiq 2026")]
    [InlineData("seleção-26")]
    [InlineData("-psiq")]
    [InlineData("psiq-")]
    [InlineData("psiq--2026")]
    [InlineData("2026-psiq")]
    public void Criar_FormatoInvalido_Recusa(string valor)
    {
        Result<IdentificadorLegivel> resultado = IdentificadorLegivel.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
    }

    [Theory(DisplayName = "Comprimento fora de 3 a 64 caracteres é recusado")]
    [InlineData("ps")]
    [InlineData("a")]
    public void Criar_CurtoDemais_Recusa(string valor)
    {
        IdentificadorLegivel.Criar(valor).Error!.Code
            .Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho);
    }

    [Fact(DisplayName = "Os limites de 3 e 64 caracteres são inclusivos; 65 é recusado")]
    public void Criar_Limites_SaoInclusivos()
    {
        IdentificadorLegivel.Criar("abc").IsSuccess.Should().BeTrue();
        IdentificadorLegivel.Criar(new string('a', 64)).IsSuccess.Should().BeTrue();
        IdentificadorLegivel.Criar(new string('a', 65)).Error!.Code
            .Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho);
    }

    [Theory(DisplayName = "Valor com a forma de um Guid é recusado, mesmo casando com o kebab-case")]
    [InlineData("a1b2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d")]
    [InlineData("abcdef01234567890abcdef012345678")]
    public void Criar_ComFormaDeGuid_Recusa(string valor)
    {
        Result<IdentificadorLegivel> resultado = IdentificadorLegivel.Criar(valor);

        resultado.IsFailure.Should().BeTrue("a rota pública por identificador precisa ser distinguível da rota por Guid");
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid);
    }
}
