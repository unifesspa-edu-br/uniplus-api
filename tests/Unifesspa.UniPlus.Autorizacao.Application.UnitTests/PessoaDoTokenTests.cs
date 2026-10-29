namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PessoaDoTokenTests
{
    [Fact(DisplayName = "Sem nome completo no token o nome fica nulo: o nome de exibição pode ser o login")]
    public void PessoaDe_SemNomeCompleto_NaoUsaONomeDeExibicao()
    {
        PessoaNoToken? pessoa = IdentidadeDoToken.PessoaDe(new Usuario("leandro.viana", null));

        pessoa!.Nome.Should().BeNull();
    }

    [Fact(DisplayName = "Com nome completo no token o nome é o dele")]
    public void PessoaDe_ComNomeCompleto_UsaONomeCompleto() =>
        IdentidadeDoToken.PessoaDe(new Usuario("leandro.viana", "Leandro Caleb Viana"))!.Nome.Should().Be("Leandro Caleb Viana");

    [Fact(DisplayName = "O nome social tem prioridade sobre o civil, e sem ele vale o civil")]
    public void NomeDe_PrefereONomeSocial()
    {
        IdentidadeDoToken.NomeDe(new Usuario("leandro.viana", "Leandro Caleb Viana", "Leandra Viana")).Should().Be("Leandra Viana");
        IdentidadeDoToken.NomeDe(new Usuario("leandro.viana", "Leandro Caleb Viana", "  ")).Should().Be("Leandro Caleb Viana");
        IdentidadeDoToken.De(new Usuario("leandro.viana", "Leandro Caleb Viana", "Leandra Viana"))!.Nome.Should().Be("Leandra Viana");
    }

    private sealed class Usuario(string login, string? nomeCompleto, string? nomeSocial = null) : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001";
        public string? Issuer => "https://idp.test/realms/unifesspa";
        public string? Jti => null;
        public string? Name => login;
        public string? NomeCompleto => nomeCompleto;
        public string? Email => null;
        public bool EmailVerificado => false;
        public string? Cpf => null;
        public string? NomeSocial => nomeSocial;
        public IReadOnlyList<string> Roles => [];
        public bool IsPlataformaAdmin => false;
        public bool HasRole(string role) => false;
        public IReadOnlyList<string> GetResourceRoles(string resourceName) => [];
    }
}
