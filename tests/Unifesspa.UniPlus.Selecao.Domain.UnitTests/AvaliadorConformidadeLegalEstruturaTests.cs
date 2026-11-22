namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class AvaliadorConformidadeLegalEstruturaTests
{
    [Fact]
    public void Avaliador_DeveSerClasseEstatica()
    {
        Type tipo = typeof(AvaliadorConformidadeLegal);
        tipo.IsClass.Should().BeTrue();
        tipo.IsAbstract.Should().BeTrue();
        tipo.IsSealed.Should().BeTrue();
    }
}
