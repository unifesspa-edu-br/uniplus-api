namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão sem alvo próprio decide sobre o tipo fixo <c>operacao</c>, e não sobre o código da permissão.
/// </summary>
public sealed class VerificadorDeAcessoHttpTests
{
    private const string Permissao = "selecao:inscricoes:consultar";

    private static readonly Guid Unidade = Guid.CreateVersion7();

    private readonly IAuthorizationDecisionService _decisao = Substitute.For<IAuthorizationDecisionService>();
    private readonly VerificadorDeAcessoHttp _verificador;

    public VerificadorDeAcessoHttpTests()
    {
        EffectiveGrant concessao = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoUnidadeId: Unidade).Value!;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "sub-opaco-123").Value!,
            "jti-abc",
            concessoesEfetivas: [concessao]).Value!;

        IAuthorizationSubjectResolver resolver = Substitute.For<IAuthorizationSubjectResolver>();
        resolver.ResolveAsync(Arg.Any<IUserContext>(), Arg.Any<AuthorizationRequestContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationSubject>.Success(sujeito));

        ICorrelationIdAccessor correlacao = Substitute.For<ICorrelationIdAccessor>();
        correlacao.CorrelationId.Returns("req-1244");

        _decisao.DecideAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(AuthorizationDecision.Permitir(concessao));

        _verificador = new VerificadorDeAcessoHttp(
            Substitute.For<IUserContext>(),
            resolver,
            _decisao,
            Substitute.For<IHttpContextAccessor>(),
            correlacao,
            TimeProvider.System);
    }

    [Fact(DisplayName = "Sem resource informado, a decisão recai sobre o tipo operacao e não sobre o código da permissão")]
    public async Task Verificar_SemResource_DecideSobreOTipoOperacao()
    {
        await _verificador.VerificarAsync(Requisito());

        await _decisao.Received(1).DecideAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == null),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A decisão por unidade recai sobre o tipo operacao, com a unidade da concessão")]
    public async Task VerificarEmAlgumaUnidade_DecideSobreOTipoOperacaoNaUnidade()
    {
        await _verificador.VerificarEmAlgumaUnidadeAsync(Requisito());

        await _decisao.Received(1).DecideAsync(
            Arg.Any<AuthorizationSubject>(),
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext>(r => r.ResourceTipo == ResourceTipos.Operacao && r.UnidadeProprietariaId == Unidade),
            Arg.Any<AuthorizationRequestContext>(),
            Arg.Any<CancellationToken>());
    }

    private static PermissionRequirement Requisito()
        => PermissionRequirement.From(Permissao, ClassificacaoDeProtecao.Interno).Value!;
}
