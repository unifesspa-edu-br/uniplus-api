namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class NomeDaCategoriaJulgadaTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_Recusa(string nome)
    {
        Action criar = () => CategoriaJulgada.Criar(Guid.NewGuid(), "RENDA", nome);
        criar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_NomeInformado_CopiaONome()
    {
        CategoriaJulgada categoria = CategoriaJulgada.Criar(Guid.NewGuid(), "RENDA", "Renda");
        categoria.Nome.Should().Be("Renda");
    }
}
