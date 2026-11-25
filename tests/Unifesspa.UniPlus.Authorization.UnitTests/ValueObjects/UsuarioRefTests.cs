namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class UsuarioRefTests
{

    [Fact]
    public void UsuarioRef_ContaIdEhTextoOpaco_NaoGuid()
    {
        // Identificadores de conta reais de Gov.br/Keycloak não têm forma de GUID.
        const string contaIdOpaco = "f:7c2b9e10-keycloak:01234567890";

        Guid.TryParse(contaIdOpaco, out _).Should().BeFalse("o contaId não deve ser um GUID");

        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", contaIdOpaco);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ContaId.Should().Be(contaIdOpaco);
    }


    [Theory]
    [InlineData(null, "conta-123")]
    [InlineData("", "conta-123")]
    [InlineData("   ", "conta-123")]
    public void UsuarioRef_EmissorVazio_Rejeita(string? emissor, string contaId)
    {
        Result<UsuarioRef> resultado = UsuarioRef.From(emissor, contaId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefEmissorObrigatorio);
    }

    [Theory]
    [InlineData("https://sso.gov.br", null)]
    [InlineData("https://sso.gov.br", "")]
    [InlineData("https://sso.gov.br", "   ")]
    public void UsuarioRef_ContaIdVazio_Rejeita(string emissor, string? contaId)
    {
        Result<UsuarioRef> resultado = UsuarioRef.From(emissor, contaId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefContaIdObrigatorio);
    }


    [Fact]
    public void UsuarioRef_EmissorEContaIdValidos_Constroi()
    {
        Guid usuarioId = Guid.CreateVersion7();

        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "conta-123", usuarioId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Emissor.Should().Be("https://sso.gov.br");
        resultado.Value.ContaId.Should().Be("conta-123");
        resultado.Value.UsuarioId.Should().Be(usuarioId);
    }

    [Fact]
    public void UsuarioRef_PreservaEmissorEContaIdVerbatim()
    {
        // Claims opacos do OIDC: espaços de fronteira fazem parte do token e
        // não podem ser normalizados — Trim aliasaria sujeitos distintos e
        // quebraria a fidelidade da auditoria por emissor + contaId.
        Result<UsuarioRef> resultado = UsuarioRef.From(" https://sso.gov.br ", " conta-123 ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Emissor.Should().Be(" https://sso.gov.br ");
        resultado.Value.ContaId.Should().Be(" conta-123 ");
    }

    [Fact]
    public void UsuarioRef_ContaIdsDiferindoPorEspaco_NaoSaoIguais()
    {
        UsuarioRef compacto = UsuarioRef.From("https://sso.gov.br", "conta-123").Value!;
        UsuarioRef comEspaco = UsuarioRef.From("https://sso.gov.br", " conta-123").Value!;

        comEspaco.Should().NotBe(compacto, "contaIds opacos distintos não podem ser aliasados por normalização");
    }

    [Fact]
    public void UsuarioRef_UsuarioIdOpcional_AceitaNulo()
    {
        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "conta-123");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UsuarioId.Should().BeNull();
    }

    [Fact]
    public void UsuarioRef_UsuarioIdGuidVazio_Rejeita()
    {
        // UsuarioId opcional aceita nulo, mas um Guid.Empty informado é malformado.
        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "conta-123", Guid.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefUsuarioIdInvalido);
    }

    // ─── Igualdade por valor ───────────────────────────────────────────────

    [Fact]
    public void UsuarioRef_MesmoEmissorContaIdEUsuarioId_SaoIguais()
    {
        Guid usuarioId = Guid.CreateVersion7();

        UsuarioRef a = UsuarioRef.From("https://sso.gov.br", "conta-123", usuarioId).Value!;
        UsuarioRef b = UsuarioRef.From("https://sso.gov.br", "conta-123", usuarioId).Value!;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
