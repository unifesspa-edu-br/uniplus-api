namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class NomeDoPerfilTests
{
    [Theory(DisplayName = "O nome sem texto, em branco, curto demais, longo demais ou com o caractere nulo não vale")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    [InlineData("Gestor\0de acessos")]
    [InlineData("\0\0\0")]
    public void Normalizar_Invalido_DevolveNulo(string? texto) =>
        NomeDoPerfil.Normalizar(texto).Should().BeNull();

    [Fact(DisplayName = "O nome vale do mínimo ao máximo de caracteres, e um a mais ou a menos não vale")]
    public void Normalizar_Fronteiras()
    {
        NomeDoPerfil.Normalizar(new string('a', NomeDoPerfil.MinimoDeCaracteres)).Should().NotBeNull();
        NomeDoPerfil.Normalizar(new string('a', NomeDoPerfil.MinimoDeCaracteres - 1)).Should().BeNull();
        NomeDoPerfil.Normalizar(new string('a', NomeDoPerfil.MaximoDeCaracteres)).Should().NotBeNull();
        NomeDoPerfil.Normalizar(new string('a', NomeDoPerfil.MaximoDeCaracteres + 1)).Should().BeNull();
    }

    [Fact(DisplayName = "O tamanho se mede depois de aparar: o máximo com espaços nas pontas vale")]
    public void Normalizar_MedeDepoisDeAparar()
    {
        NomeDoPerfil? nome = NomeDoPerfil.Normalizar($"  {new string('a', NomeDoPerfil.MaximoDeCaracteres)}  ");

        nome.Should().NotBeNull();
        nome!.Valor.Should().HaveLength(NomeDoPerfil.MaximoDeCaracteres);
    }

    [Fact(DisplayName = "O nome sai sem espaços nas pontas e mantém o miolo como veio")]
    public void Normalizar_AparaAsPontas()
    {
        NomeDoPerfil? nome = NomeDoPerfil.Normalizar("  Gestor de acessos \t");

        nome!.Valor.Should().Be("Gestor de acessos");
        nome.ToString().Should().Be("Gestor de acessos");
    }

    [Fact(DisplayName = "O nome conta unidades de texto, como a coluna: o caractere fora do plano básico vale por duas")]
    public void Normalizar_ContaUnidadesDeTexto()
    {
        string noLimite = new string('a', NomeDoPerfil.MaximoDeCaracteres - 2) + "😀";

        NomeDoPerfil.Normalizar(noLimite).Should().NotBeNull();
        NomeDoPerfil.Normalizar(noLimite + "a").Should().BeNull();
    }

    [Fact(DisplayName = "O nome que o cadastro já guarda se reidrata como veio, sem conferir de novo")]
    public void Reidratar_MantemOValor()
    {
        NomeDoPerfil.Reidratar("ab").Valor.Should().Be("ab");
    }

    [Theory(DisplayName = "O nome reidratado exige que exista")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Reidratar_SemTexto_Recusa(string? persistido)
    {
        Action reidratar = () => NomeDoPerfil.Reidratar(persistido!);

        reidratar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Dois nomes com o mesmo texto são iguais")]
    public void Igualdade_PorValor() =>
        NomeDoPerfil.Reidratar("Gestor de acessos").Should().Be(NomeDoPerfil.Reidratar("Gestor de acessos"));
}
