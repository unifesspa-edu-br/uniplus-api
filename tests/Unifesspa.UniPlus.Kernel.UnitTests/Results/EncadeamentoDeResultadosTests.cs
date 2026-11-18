namespace Unifesspa.UniPlus.Kernel.UnitTests.Results;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed class EncadeamentoDeResultadosTests
{
    [Fact]
    public void Encadear_Sucesso_InvocaProximaParte()
    {
        Result<string> resultado = Result<int>.Success(3).Encadear(valor => Result<string>.Success(new string('a', valor)));

        resultado.Value.Should().Be("aaa");
    }

    [Fact]
    public void Encadear_Falha_PreservaErroESuspendeProximaParte()
    {
        DomainError erro = new("Teste.Falha", "Falha");
        bool executou = false;
        Result<string> resultado = Result<int>.Failure(erro).Encadear(valor =>
        {
            executou = true;
            return Result<string>.Success(valor.ToString(CultureInfo.InvariantCulture));
        });

        resultado.Error.Should().Be(erro);
        executou.Should().BeFalse();
    }

    [Fact]
    public void Compor_Sucesso_ProduzValor()
    {
        Result<int> resultado = Result<int>.Success(3).Compor(valor => valor * 2);

        resultado.Value.Should().Be(6);
    }

    [Fact]
    public void Compor_Falha_PreservaErroESuspendeComposicao()
    {
        DomainError erro = new("Teste.Falha", "Falha");
        bool executou = false;
        Result<string> resultado = Result<int>.Failure(erro).Compor(valor =>
        {
            executou = true;
            return valor.ToString(CultureInfo.InvariantCulture);
        });

        resultado.Error.Should().Be(erro);
        executou.Should().BeFalse();
    }
}
