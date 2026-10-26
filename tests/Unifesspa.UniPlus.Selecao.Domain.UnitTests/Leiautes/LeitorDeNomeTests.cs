namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Leiautes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>O nome que passa do limite é cortado sem partir um par substituto, que o banco recusaria.</summary>
public sealed class LeitorDeNomeTests
{
    [Fact(DisplayName = "nome cortado no meio de um par substituto recua um caractere")]
    public void Ler_CorteNoMeioDoParSubstituto_RecuaUm()
    {
        string nome = new string('a', LeitorDeNome.TamanhoMaximo - 1) + "😀" + "b";

        string? lido = (string?)new LeitorDeNome().Ler(nome).Valor;

        lido.Should().Be(new string('a', LeitorDeNome.TamanhoMaximo - 1));
    }
}
