namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DualApprovalGrantTests
{
    private const string Emissor = "https://sso.gov.br";
    private const string ResourceHash = "sha256:abc123";
    private const string Permissao = "selecao.resultado.homologar";

    private static readonly DateTimeOffset InstanteDaConcessao = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static UsuarioRef Aprovador(string contaId) => UsuarioRef.From(Emissor, contaId).Value!;

    // ─── CA-05: aprovador secundário igual ao primário é rejeitado ─────────

    [Fact]
    public void DualApprovalGrant_SecundarioIgualPrimario_Rejeita()
    {
        UsuarioRef primario = Aprovador("conta-1");

        // Mesma identidade OIDC (emissor + contaId), ainda que outra instância
        // e com UsuarioId distinto — deve ser tratado como o mesmo aprovador.
        UsuarioRef secundario = UsuarioRef.From(Emissor, "conta-1", Guid.CreateVersion7()).Value!;

        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            primario, secundario, ResourceHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalAprovadoresIguais);
    }

    // ─── CA-05: validade acima do limite de 1h é rejeitada ─────────────────

    [Fact]
    public void DualApprovalGrant_ValidadeAcimaDoLimite_Rejeita()
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"),
            Aprovador("conta-2"),
            ResourceHash,
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
        UsuarioRef primario = Aprovador("conta-1");
        UsuarioRef secundario = Aprovador("conta-2");
        DateTimeOffset concedidoEm = InstanteDaConcessao;
        DateTimeOffset validoAte = concedidoEm.Add(DualApprovalGrant.JanelaMaxima); // exatamente no limite (1h)
        DateTimeOffset usadoEm = concedidoEm.AddMinutes(10);

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            primario, secundario, ResourceHash, Permissao, concedidoEm, validoAte,
            usado: true, usadoEm: usadoEm, usadoPor: secundario);

        resultado.IsSuccess.Should().BeTrue();
        VerificarConcessaoUsada(resultado.Value!, primario, secundario, validoAte, usadoEm);
    }

    private static void VerificarConcessaoUsada(
        DualApprovalGrant concessao, UsuarioRef primario, UsuarioRef secundario, DateTimeOffset validoAte, DateTimeOffset usadoEm)
    {
        concessao.AprovadorPrimario.Should().Be(primario);
        concessao.AprovadorSecundario.Should().Be(secundario);
        concessao.ValidoAte.Should().Be(validoAte);
        concessao.Usado.Should().BeTrue();
        concessao.UsadoEm.Should().Be(usadoEm);
        concessao.UsadoPor.Should().Be(secundario);
    }

    [Fact]
    public void DualApprovalGrant_IdentificadorEhGuidV7()
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"), Aprovador("conta-2"), ResourceHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Id.Should().NotBe(Guid.Empty);
        resultado.Value.Id.Version.Should().Be(7, "o identificador segue a ADR-0032 (GUID v7)");
    }

    [Fact]
    public void DualApprovalGrant_NaoUsado_PorPadrao()
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"), Aprovador("conta-2"), ResourceHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Usado.Should().BeFalse();
        resultado.Value.UsadoEm.Should().BeNull();
        resultado.Value.UsadoPor.Should().BeNull();
    }

    [Fact]
    public void DualApprovalGrant_ValidadeNaoPosteriorAConcessao_Rejeita()
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"), Aprovador("conta-2"), ResourceHash, Permissao, concedidoEm, concedidoEm);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalValidadeNaoPosterior);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DualApprovalGrant_PermissaoVazia_Rejeita(string? permissao)
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"), Aprovador("conta-2"), ResourceHash, permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalPermissaoObrigatoria);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DualApprovalGrant_ResourceHashVazio_Rejeita(string? resourceHash)
    {
        DateTimeOffset concedidoEm = InstanteDaConcessao;

        Result<DualApprovalGrant> resultado = DualApprovalGrant.From(
            Aprovador("conta-1"), Aprovador("conta-2"), resourceHash, Permissao, concedidoEm, concedidoEm.AddMinutes(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.DualApprovalResourceHashObrigatorio);
    }
}
