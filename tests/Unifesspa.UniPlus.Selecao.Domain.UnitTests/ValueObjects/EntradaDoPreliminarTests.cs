namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A entrada do preliminar é um valor: duas entradas com as mesmas linhas são iguais, qualquer que seja a ordem em que chegam.</summary>
public sealed class EntradaDoPreliminarTests
{
    [Fact(DisplayName = "Entradas com as mesmas linhas são iguais e têm o mesmo código de dispersão")]
    public void MesmasLinhas_SaoIguais()
    {
        EntradaDoPreliminar primeira = new(["peso=1", "inicio=2027-03-01"]);
        EntradaDoPreliminar segunda = new(["peso=1", "inicio=2027-03-01"]);

        segunda.Should().Be(primeira);
        segunda.GetHashCode().Should().Be(primeira.GetHashCode());
    }

    [Fact(DisplayName = "A ordem em que as linhas chegam não conta: elas ficam em ordem ordinal")]
    public void OrdemDeChegada_NaoConta()
    {
        EntradaDoPreliminar naOrdem = new(["a=1", "b=2"]);
        EntradaDoPreliminar invertida = new(["b=2", "a=1"]);

        invertida.Should().Be(naOrdem);
        invertida.Linhas.Should().Equal("a=1", "b=2");
    }

    [Fact(DisplayName = "Entradas com uma linha diferente não são iguais")]
    public void LinhaDiferente_NaoESaoIguais() =>
        new EntradaDoPreliminar(["peso=1"]).Should().NotBe(new EntradaDoPreliminar(["peso=2"]));

    [Fact(DisplayName = "A linha repetida conta: a mesma linha duas vezes não é a mesma entrada que uma só")]
    public void LinhaRepetida_Conta() =>
        new EntradaDoPreliminar(["peso=1", "peso=1"]).Should().NotBe(new EntradaDoPreliminar(["peso=1"]));

    [Fact(DisplayName = "Entrada sem linhas é igual a outra sem linhas")]
    public void SemLinhas_SaoIguais() =>
        new EntradaDoPreliminar([]).Should().Be(new EntradaDoPreliminar([]));
}
