namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class UsuarioRefTests
{
    // ─── CA-02: Subject é texto opaco, nunca GUID ──────────────────────────

    [Fact]
    public void UsuarioRef_SubjectEhTextoOpaco_NaoGuid()
    {
        // Subjects reais de Gov.br/Keycloak não têm forma de GUID.
        const string subjectOpaco = "f:7c2b9e10-keycloak:01234567890";

        Guid.TryParse(subjectOpaco, out _).Should().BeFalse("o subject não deve ser um GUID");

        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", subjectOpaco);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Subject.Should().Be(subjectOpaco);

        typeof(UsuarioRef).GetProperty(nameof(UsuarioRef.Subject))!.PropertyType
            .Should().Be<string>("o subject é estruturalmente uma string opaca, nunca um Guid");
    }

    // ─── CA-02: emissor/subject vazios são rejeitados ──────────────────────

    [Theory]
    [InlineData(null, "sub-123")]
    [InlineData("", "sub-123")]
    [InlineData("   ", "sub-123")]
    public void UsuarioRef_EmissorVazio_Rejeita(string? emissor, string subject)
    {
        Result<UsuarioRef> resultado = UsuarioRef.From(emissor, subject);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefEmissorObrigatorio);
    }

    [Theory]
    [InlineData("https://sso.gov.br", null)]
    [InlineData("https://sso.gov.br", "")]
    [InlineData("https://sso.gov.br", "   ")]
    public void UsuarioRef_SubjectVazio_Rejeita(string emissor, string? subject)
    {
        Result<UsuarioRef> resultado = UsuarioRef.From(emissor, subject);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefSubjectObrigatorio);
    }

    // ─── CA-02: contraprova positiva ───────────────────────────────────────

    [Fact]
    public void UsuarioRef_EmissorESubjectValidos_Constroi()
    {
        Guid usuarioId = Guid.CreateVersion7();

        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "sub-123", usuarioId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Emissor.Should().Be("https://sso.gov.br");
        resultado.Value.Subject.Should().Be("sub-123");
        resultado.Value.UsuarioId.Should().Be(usuarioId);
    }

    [Fact]
    public void UsuarioRef_PreservaEmissorESubjectVerbatim()
    {
        // Claims opacos do OIDC: espaços de fronteira fazem parte do token e
        // não podem ser normalizados — Trim aliasaria sujeitos distintos e
        // quebraria a fidelidade da auditoria por emissor + subject.
        Result<UsuarioRef> resultado = UsuarioRef.From(" https://sso.gov.br ", " sub-123 ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Emissor.Should().Be(" https://sso.gov.br ");
        resultado.Value.Subject.Should().Be(" sub-123 ");
    }

    [Fact]
    public void UsuarioRef_SubjectsDiferindoPorEspaco_NaoSaoIguais()
    {
        UsuarioRef compacto = UsuarioRef.From("https://sso.gov.br", "sub-123").Value!;
        UsuarioRef comEspaco = UsuarioRef.From("https://sso.gov.br", " sub-123").Value!;

        comEspaco.Should().NotBe(compacto, "subjects opacos distintos não podem ser aliasados por normalização");
    }

    [Fact]
    public void UsuarioRef_UsuarioIdOpcional_AceitaNulo()
    {
        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "sub-123");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UsuarioId.Should().BeNull();
    }

    [Fact]
    public void UsuarioRef_UsuarioIdGuidVazio_Rejeita()
    {
        // UsuarioId opcional aceita nulo, mas um Guid.Empty informado é malformado.
        Result<UsuarioRef> resultado = UsuarioRef.From("https://sso.gov.br", "sub-123", Guid.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.UsuarioRefUsuarioIdInvalido);
    }

    // ─── Igualdade por valor ───────────────────────────────────────────────

    [Fact]
    public void UsuarioRef_MesmoEmissorSubjectEUsuarioId_SaoIguais()
    {
        Guid usuarioId = Guid.CreateVersion7();

        UsuarioRef a = UsuarioRef.From("https://sso.gov.br", "sub-123", usuarioId).Value!;
        UsuarioRef b = UsuarioRef.From("https://sso.gov.br", "sub-123", usuarioId).Value!;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
