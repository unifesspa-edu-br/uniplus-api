namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Security.Claims;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Montagem do sujeito da decisão a partir do token: identidade, sessão e as
/// concessões derivadas dos papéis do <i>client</i> da API.
/// </summary>
public sealed class HttpAuthorizationSubjectResolverTests
{
    // Os dois valores são distintos de propósito (ADR-0010): confundi-los faz o
    // resolver procurar os papéis sob uma chave que o token não usa.
    private const string ClientDaApi = "uniplus-api";
    private const string AudienciaDoToken = "uniplus";
    private const string Emissor = "https://idp.exemplo/realms/unifesspa";
    private const string Permissao = "configuracao:motivos-decisao-recursal:manter";

    [Fact]
    public async Task Resolve_ComTokenCompleto_MontaSujeitoComConcessoesDoClient()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            ResourceAccess((ClientDaApi, [Permissao])));

        AuthorizationSubject sujeito = resultado.Value!;
        sujeito.Usuario.Emissor.Should().Be(Emissor);
        sujeito.Usuario.Subject.Should().Be("sub-opaco-1");
        sujeito.Jti.Should().Be("jti-1");
        sujeito.ConcessoesEfetivas.Should().ContainSingle()
            .Which.Should().Match<Unifesspa.UniPlus.Authorization.ValueObjects.EffectiveGrant>(
                concessao => concessao.PermissaoCodigo == Permissao && concessao.Fonte == FonteGrant.Token);
    }

    [Fact]
    public async Task Resolve_PapeisSobAAudiencia_NaoViramConcessao()
    {
        // A audiência é a claim compartilhada da plataforma; os papéis desta API
        // ficam sob o clientId. Procurar sob a audiência não acha nada — e o
        // efeito, em ambiente real, é negar toda decisão em silêncio (ADR-0010).
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            ResourceAccess((AudienciaDoToken, [Permissao])));

        resultado.Value!.ConcessoesEfetivas.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_PapeisDeOutroClient_NaoViramConcessao()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            ResourceAccess(("outro-sistema", [Permissao])));

        resultado.Value!.ConcessoesEfetivas.Should().BeEmpty(
            "permissão concedida no client de outro sistema não vale nesta API");
    }

    [Fact]
    public async Task Resolve_PapeisDeRealm_NaoViramConcessao()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { Permissao } })));

        resultado.Value!.ConcessoesEfetivas.Should().BeEmpty(
            "papel de realm representa responsabilidade institucional ampla, não permissão "
            + "— misturar as listas faria um papel administrativo conceder qualquer permissão homônima");
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

    [Fact]
    public async Task Resolve_ResourceAccessMalformado_FalhaSemConcessoes()
    {
        Result<AuthorizationSubject> resultado = await Resolver(
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            new Claim("resource_access", "isto-nao-e-json"));

        resultado.IsSuccess.Should().BeTrue("um claim ilegível não invalida a identidade");
        resultado.Value!.ConcessoesEfetivas.Should().BeEmpty(
            "sem conseguir ler os papéis, nenhuma concessão pode ser afirmada");
    }

    private static Claim ResourceAccess(params (string Client, string[] Roles)[] clients)
    {
        Dictionary<string, object> mapa = clients.ToDictionary(
            static item => item.Client,
            static object (item) => new { roles = item.Roles },
            StringComparer.Ordinal);

        return new Claim("resource_access", JsonSerializer.Serialize(mapa));
    }

    private static Task<Result<AuthorizationSubject>> Resolver(params Claim[] claims)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
        };

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return Resolver(new HttpUserContext(accessor, NullLogger<HttpUserContext>.Instance));
    }

    private static Task<Result<AuthorizationSubject>> Resolver(HttpUserContext userContext)
    {
        HttpAuthorizationSubjectResolver resolver = new(Options.Create(new AuthOptions
        {
            Authority = "https://idp.exemplo/realms/unifesspa",
            Audience = AudienciaDoToken,
            ClientId = ClientDaApi,
        }));

        return resolver.ResolveAsync(
            userContext,
            AuthorizationRequestContext.From("req-1", DateTimeOffset.UnixEpoch, OrigemRequisicao.Api).Value!,
            CancellationToken.None);
    }
}
