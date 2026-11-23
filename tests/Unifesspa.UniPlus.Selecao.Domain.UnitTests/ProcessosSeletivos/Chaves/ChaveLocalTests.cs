namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Chaves;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChaveLocalTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("etapa_prova_escrita_2")]
    public void Criar_ComFormatoValido_Aceita(string valor)
    {
        ChaveLocal.Criar(valor).Value.Valor.Should().Be(valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Etapa")]
    [InlineData("1etapa")]
    [InlineData("etapa__x")]
    [InlineData("etapa_")]
    [InlineData("prova-objetiva")]
    [InlineData("etapa\n")]
    public void Criar_ComFormatoInvalido_Recusa(string? valor)
    {
        ChaveLocal.Criar(valor).Error!.Code.Should().Be(ChaveLocal.CodigoDeErro);
    }

    [Fact]
    public void Criar_ComSessentaEQuatroCaracteres_Aceita()
    {
        string valor = new('a', 64);
        ChaveLocal.Criar(valor).Value.Valor.Should().Be(valor);
    }

    [Fact]
    public void Criar_ComSessentaECincoCaracteres_Recusa()
    {
        ChaveLocal.Criar(new string('a', 65)).Error!.Code.Should().Be(ChaveLocal.CodigoDeErro);
    }

    [Fact]
    public void Adicionar_ComChaveRepetida_ConservaUnicidade()
    {
        var chaves = new ChavesEmUso();
        ChaveLocal chave = ChaveLocal.Criar("item_a").Value;
        chaves.Adicionar(chave).Should().BeTrue();
        chaves.Adicionar(chave).Should().BeFalse();
        chaves.Contem(chave).Should().BeTrue();
    }
}
