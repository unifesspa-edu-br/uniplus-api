namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class UnidadeResponsavelSnapshotTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_CodigoVazio_Recusa(string codigo)
    {
        Action criar = () => _ = new UnidadeResponsavelSnapshot(Guid.CreateVersion7(), codigo, "SIGLA", "Unidade fictícia");

        criar.Should().Throw<ArgumentException>();
    }
}
