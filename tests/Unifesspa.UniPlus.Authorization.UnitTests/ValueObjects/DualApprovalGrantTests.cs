namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DualApprovalGrantTests
{
    private const string Emissor = "https://sso.gov.br";
    private const string RecursoHash = "sha256:abc123";
    private const string Permissao = "selecao.resultado.homologar";

    private static UsuarioRef Aprovador(string subject) => UsuarioRef.From(Emissor, subject).Value!;

    // ─── CA-05: aprovador secundário igual ao primário é rejeitado ─────────

    [Fact]
    public void DualApprovalGrant_SecundarioIgualPrimario_Rejeita()
    {
        UsuarioRef primario = Aprovador("sub-1");

        // Mesma identidade OIDC (emissor + subject), ainda que outra instância
        // e com UsuarioId distinto — deve ser tratado como o mesmo aprovador.
        UsuarioRef secundario = UsuarioRef.From(Emissor, "sub-1", Guid.CreateVersion7()).Value!;

        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            primario, secundario, RecursoHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalAprovadoresIguais);
    }

    // ─── CA-05: validade acima do limite de 1h é rejeitada ─────────────────

    [Fact]
    public void DualApprovalGrant_ValidadeAcimaDoLimite_Rejeita()
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"),
            Aprovador("sub-2"),
            RecursoHash,
            Permissao,
            concedidoEm,
            concedidoEm.AddHours(1).AddSeconds(1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalValidadeAcimaDoLimite);
    }

    // ─── CA-05: contraprova — distintos, validade no limite, marcador de uso ──

    [Fact]
    public void DualApprovalGrant_AprovadoresDistintos_Constroi()
    {
        UsuarioRef primario = Aprovador("sub-1");
        UsuarioRef secundario = Aprovador("sub-2");
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;
        DateTimeOffset validoAte = concedidoEm.Add(DualApprovalGrant.JanelaMaxima); // exatamente no limite (1h)
        DateTimeOffset usadoEm = concedidoEm.AddMinutes(10);

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            primario, secundario, RecursoHash, Permissao, concedidoEm, validoAte,
            usado: true, usadoEm: usadoEm, usadoPor: secundario);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.AprovadorPrimario.Should().Be(primario);
        resultado.Value.AprovadorSecundario.Should().Be(secundario);
        resultado.Value.ValidoAte.Should().Be(validoAte);
        resultado.Value.Usado.Should().BeTrue();
        resultado.Value.UsadoEm.Should().Be(usadoEm);
        resultado.Value.UsadoPor.Should().Be(secundario);
    }

    [Fact]
    public void DualApprovalGrant_IdentificadorEhGuidV7()
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"), Aprovador("sub-2"), RecursoHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Id.Should().NotBe(Guid.Empty);
        resultado.Value.Id.Version.Should().Be(7, "o identificador segue a ADR-0032 (GUID v7)");
    }

    [Fact]
    public void DualApprovalGrant_NaoUsado_PorPadrao()
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"), Aprovador("sub-2"), RecursoHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Usado.Should().BeFalse();
        resultado.Value.UsadoEm.Should().BeNull();
        resultado.Value.UsadoPor.Should().BeNull();
    }

    [Fact]
    public void DualApprovalGrant_ValidadeNaoPosteriorAConcessao_Rejeita()
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"), Aprovador("sub-2"), RecursoHash, Permissao, concedidoEm, concedidoEm);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalValidadeNaoPosterior);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DualApprovalGrant_PermissaoVazia_Rejeita(string? permissao)
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"), Aprovador("sub-2"), RecursoHash, permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalPermissaoObrigatoria);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DualApprovalGrant_RecursoHashVazio_Rejeita(string? recursoHash)
    {
        DateTimeOffset concedidoEm = DateTimeOffset.UtcNow;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("sub-1"), Aprovador("sub-2"), recursoHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalRecursoHashObrigatorio);
    }
}
