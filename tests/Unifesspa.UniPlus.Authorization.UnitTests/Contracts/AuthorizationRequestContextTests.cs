namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AuthorizationRequestContextTests
{
    private const string RequestId = "req-7f3a";

    // ─── CA-06: dupla aprovação ausente quando a operação não exige (contraprova) ──

    [Fact]
    public void AuthorizationRequestContext_DuplaAprovacaoOpcional_AusenteQuandoNaoExigida()
    {
        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            RequestId, DateTimeOffset.UtcNow, OrigemRequisicao.Api);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DuplaAprovacao.Should().BeNull("o tipo não obriga o preenchimento da dupla aprovação");
    }

    // ─── CA-06: dupla aprovação presente quando a operação exige (contraprova) ──

    [Fact]
    public void AuthorizationRequestContext_DuplaAprovacaoPresente_QuandoExigida()
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        DualApprovalGrant grant = DualApprovalGrant.From(
            UsuarioRef.From("https://sso.gov.br", "sub-1").Value!,
            UsuarioRef.From("https://sso.gov.br", "sub-2").Value!,
            "sha256:abc",
            "selecao.resultado.homologar",
            agora,
            agora.AddMinutes(20)).Value!;

        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            RequestId, agora, OrigemRequisicao.Api, duplaAprovacao: grant);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DuplaAprovacao.Should().Be(grant);
    }

    // ─── DataAcesso em UTC ─────────────────────────────────────────────────

    [Fact]
    public void AuthorizationRequestContext_DataAcesso_NormalizadaParaUtc()
    {
        var local = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.FromHours(-3));

        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            RequestId, local, OrigemRequisicao.Jobs);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DataAcesso.Offset.Should().Be(TimeSpan.Zero, "o instante de acesso é sempre UTC");
        resultado.Value.DataAcesso.Should().Be(local.ToUniversalTime(), "a normalização preserva o instante");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizationRequestContext_RequestIdVazio_Rejeita(string? requestId)
    {
        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            requestId, DateTimeOffset.UtcNow, OrigemRequisicao.Api);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.AuthorizationRequestContextRequestIdObrigatorio);
    }

    [Fact]
    public void AuthorizationRequestContext_OnBehalfOfGuidVazio_Rejeita()
    {
        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            RequestId, DateTimeOffset.UtcNow, OrigemRequisicao.Api, onBehalfOfId: Guid.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.AuthorizationRequestContextOnBehalfOfInvalido);
    }

    [Fact]
    public void AuthorizationRequestContext_IpEUserAgentNulos_ViramVazios()
    {
        Result<AuthorizationRequestContext> resultado = AuthorizationRequestContext.From(
            RequestId, DateTimeOffset.UtcNow, OrigemRequisicao.AdminCli);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.IpOrigem.Should().BeEmpty();
        resultado.Value.UserAgent.Should().BeEmpty();
    }
}
