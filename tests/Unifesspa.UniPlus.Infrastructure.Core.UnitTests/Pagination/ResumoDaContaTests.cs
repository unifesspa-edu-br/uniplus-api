namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// O vínculo do cursor à conta leva um resumo do par emissor e conta, não a identificação: contas diferentes, ou a
/// mesma conta em outro emissor, não se confundem.
/// </summary>
public sealed class ResumoDaContaTests
{
    [Fact(DisplayName = "A mesma conta do mesmo emissor dá sempre o mesmo resumo, de 43 caracteres")]
    public void Resumir_MesmaConta_MesmoResumo()
    {
        string? primeiro = ContaDeQuemConsulta.Resumir(Conta("emissor-a", "conta-1"));
        string? segundo = ContaDeQuemConsulta.Resumir(Conta("emissor-a", "conta-1"));

        primeiro.Should().Be(segundo).And.HaveLength(43);
    }

    [Fact(DisplayName = "Outra conta ou outro emissor dá outro resumo")]
    public void Resumir_OutraContaOuEmissor_ResumoDiferente()
    {
        string? base1 = ContaDeQuemConsulta.Resumir(Conta("emissor-a", "conta-1"));

        ContaDeQuemConsulta.Resumir(Conta("emissor-a", "conta-2")).Should().NotBe(base1);
        ContaDeQuemConsulta.Resumir(Conta("emissor-b", "conta-1")).Should().NotBe(base1);
    }

    [Fact(DisplayName = "O resumo não carrega a identificação da conta")]
    public void Resumir_NaoExpoeAIdentificacao() =>
        ContaDeQuemConsulta.Resumir(Conta("emissor-a", "conta-1")).Should().NotContain("conta-1");

    [Theory(DisplayName = "Sem conta autenticada com emissor e identificador não há resumo")]
    [InlineData(false, "emissor-a", "conta-1")]
    [InlineData(true, null, "conta-1")]
    [InlineData(true, "", "conta-1")]
    [InlineData(true, "emissor-a", null)]
    [InlineData(true, "emissor-a", "")]
    public void Resumir_ContaIncompleta_Nulo(bool autenticada, string? emissor, string? conta) =>
        ContaDeQuemConsulta.Resumir(Conta(emissor, conta, autenticada)).Should().BeNull();

    private static IUserContext Conta(string? emissor, string? conta, bool autenticada = true)
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.IsAuthenticated.Returns(autenticada);
        usuario.Issuer.Returns(emissor);
        usuario.UserId.Returns(conta);
        return usuario;
    }
}
