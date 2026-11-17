namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.Entities;

public sealed class ResultadoDaGravacaoTests
{
    [Fact]
    public void Somar_junta_cada_contagem_e_mantem_os_escritos()
    {
        ResultadoDaGravacao soma = new ResultadoDaGravacao(1, 2, 3).Somar(new ResultadoDaGravacao(10, 20, 30));

        soma.Should().Be(new ResultadoDaGravacao(11, 22, 33));
        soma.Escritos.Should().Be(33);
    }

    [Fact]
    public void Somar_exige_o_outro_resultado()
    {
        Action somar = () => new ResultadoDaGravacao(0, 0, 0).Somar(null!);

        somar.Should().Throw<ArgumentNullException>();
    }
}
