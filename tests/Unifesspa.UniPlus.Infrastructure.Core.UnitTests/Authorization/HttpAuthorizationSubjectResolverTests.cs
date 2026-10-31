namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Security.Claims;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Montagem do sujeito da decisão: identidade e sessão vêm do token; as concessões vêm só das
/// gravadas no Uni+. Nenhum papel do token vira concessão.
/// </summary>
public sealed class HttpAuthorizationSubjectResolverTests
{
    private const string ClientDaApi = "uniplus-api";
    private const string Emissor = "https://idp.exemplo/realms/unifesspa";
    private const string Permissao = "selecao:motivos-decisao-isencao:manter";

    private static readonly Guid Unidade = Guid.CreateVersion7();

    [Fact]
    public async Task Resolve_ComTokenCompleto_MontaSujeitoComAsConcessoesGravadas()
    {
        Guid concessaoId = Guid.CreateVersion7();
        Guid perfilId = Guid.CreateVersion7();
        DateTimeOffset validade = DateTimeOffset.UnixEpoch.AddDays(30);

        Result<AuthorizationSubject> resultado = await Resolver(
            [
                new ConcessaoAtiva(concessaoId, perfilId, "Perfil", Unidade, false, validade, [Permissao, "selecao:outra-coisa:ler"]),
            ],
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        AuthorizationSubject sujeito = resultado.Value!;
        sujeito.Usuario.Emissor.Should().Be(Emissor);
        sujeito.Usuario.Subject.Should().Be("sub-opaco-1");
        sujeito.Jti.Should().Be("jti-1");
        sujeito.ConcessoesEfetivas.Select(c => c.PermissaoCodigo).Should().Equal(Permissao, "selecao:outra-coisa:ler");
        sujeito.ConcessoesEfetivas.Should().AllSatisfy(concessao =>
        {
            concessao.Fonte.Should().Be(FonteGrant.Concessao);
            concessao.GrantId.Should().Be(concessaoId);
            concessao.PerfilId.Should().Be(perfilId);
            concessao.EscopoUnidadeId.Should().Be(Unidade);
            concessao.EscopoInstitucional.Should().BeFalse();
            concessao.ValidoAte.Should().Be(validade);
        });
    }

    [Fact]
    public async Task Resolve_ConcessaoInstitucional_NaoTemUnidade()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            [new ConcessaoAtiva(Guid.CreateVersion7(), Guid.CreateVersion7(), "Perfil", null, true, null, [Permissao])],
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        EffectiveGrant concessao = resultado.Value!.ConcessoesEfetivas.Should().ContainSingle().Subject;
        concessao.EscopoInstitucional.Should().BeTrue();
        concessao.EscopoUnidadeId.Should().BeNull();
        concessao.ValidoAte.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ConcessaoVencida_ContinuaNoSujeitoParaADecisaoRecusarComoExpirada()
    {
        DateTimeOffset vencida = DateTimeOffset.UnixEpoch.AddDays(-1);

        Result<AuthorizationSubject> resultado = await Resolver(
            [new ConcessaoAtiva(Guid.CreateVersion7(), Guid.CreateVersion7(), "Perfil", null, true, vencida, [Permissao])],
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        resultado.Value!.ConcessoesEfetivas.Should().ContainSingle().Which.ValidoAte.Should().Be(vencida);
    }

    [Fact]
    public async Task Resolve_PermissaoEmBrancoNoPerfil_NaoInvalidaAsDemais()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            [new ConcessaoAtiva(Guid.CreateVersion7(), Guid.CreateVersion7(), "Perfil", null, true, null, [" ", Permissao])],
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        resultado.Value!.ConcessoesEfetivas.Should().ContainSingle().Which.PermissaoCodigo.Should().Be(Permissao);
    }

    [Fact]
    public async Task Resolve_PapeisNoToken_NaoViramConcessao()
    {
        // Papel de client, de realm e o papel mais amplo da plataforma: nenhum concede, porque o
        // token identifica a pessoa e a permissão vem da concessão gravada.
        Result<AuthorizationSubject> resultado = await Resolver(
            [],
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            new Claim("resource_access", JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [ClientDaApi] = new { roles = new[] { Permissao, "uniplus-instalacao" } },
            })),
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { Permissao, "plataforma-admin" } })));

        resultado.Value!.ConcessoesEfetivas.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_ConsultaAsConcessoesDaPessoaDoToken()
    {
        IConcessoesAtivasDoSujeito concessoes = Substitute.For<IConcessoesAtivasDoSujeito>();
        concessoes.ListarAsync(Arg.Any<UsuarioRef>(), Arg.Any<CancellationToken>()).Returns([]);

        await Resolver(
            concessoes,
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        await concessoes.Received(1).ListarAsync(
            Arg.Is<UsuarioRef>(u => u.Emissor == Emissor && u.Subject == "sub-opaco-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_SemJti_Falha()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.SujeitoJtiAusente);
    }

    [Fact]
    public async Task Resolve_SemIssuer_Falha()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.SujeitoIssuerAusente);
    }

    [Fact]
    public async Task Resolve_SemSubject_Falha()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("jti", "jti-1"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.SujeitoSubjectAusente);
    }

    [Fact]
    public async Task Resolve_RequisicaoAnonima_FalhaSemLancar()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        Result<AuthorizationSubject> resultado = await Resolver(
            new HttpUserContext(accessor, NullLogger<HttpUserContext>.Instance));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.SujeitoNaoAutenticado);
    }

    private static Task<Result<AuthorizationSubject>> Resolver(params Claim[] claims) =>
        Resolver([], claims);

    private static Task<Result<AuthorizationSubject>> Resolver(IReadOnlyList<ConcessaoAtiva> gravadas, params Claim[] claims)
    {
        IConcessoesAtivasDoSujeito concessoes = Substitute.For<IConcessoesAtivasDoSujeito>();
        concessoes.ListarAsync(Arg.Any<UsuarioRef>(), Arg.Any<CancellationToken>()).Returns(gravadas);

        return Resolver(concessoes, claims);
    }

    private static Task<Result<AuthorizationSubject>> Resolver(IConcessoesAtivasDoSujeito concessoes, params Claim[] claims)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
        };

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return Resolver(new HttpUserContext(accessor, NullLogger<HttpUserContext>.Instance), concessoes);
    }

    private static Task<Result<AuthorizationSubject>> Resolver(HttpUserContext userContext, IConcessoesAtivasDoSujeito? concessoes = null)
    {
        HttpAuthorizationSubjectResolver resolver = new(concessoes ?? Substitute.For<IConcessoesAtivasDoSujeito>());

        return resolver.ResolveAsync(
            userContext,
            AuthorizationRequestContext.From("req-1", DateTimeOffset.UnixEpoch, OrigemRequisicao.Api).Value!,
            CancellationToken.None);
    }
}
