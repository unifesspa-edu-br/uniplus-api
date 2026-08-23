namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Prova o ponto de decisão único montado pelo composition root real: os
/// serviços saem do contêiner do host, o sujeito é montado de um token e a
/// decisão sai do mesmo caminho que uma requisição percorreria.
/// </summary>
/// <remarks>
/// Os testes de unidade cobrem a regra; este cobre o que só o host responde —
/// que <c>AddAutorizacao</c> registra o que a decisão precisa, que o
/// <i>client</i> conferido é a audiência configurada e que subir a aplicação com
/// a configuração versionada não quebra por causa do registro operacional.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PdpMinimoNoHostTests
{
    private const string Emissor = "https://idp.exemplo/realms/unifesspa";
    private const string Permissao = UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManter;

    private readonly MonolitoPostgresFixture _fixture;

    public PdpMinimoNoHostTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O host resolve o ponto de decisão e o agregador do sujeito")]
    public void Host_RegistraOsServicosDaDecisao()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<IAuthorizationDecisionService>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IAuthorizationSubjectResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IRegistroOperacionalRestrito>().Should().NotBeNull();

        // Resolver de fato, e não só consultar o registro: o verificador depende
        // de peças que outras extensões registram (contexto do usuário, acessor
        // de correlação, relógio). Uma delas ausente só apareceria na primeira
        // requisição que tentasse decidir.
        scope.ServiceProvider.Invoking(sp => sp.GetRequiredService<IVerificadorDeAcesso>())
            .Should().NotThrow();
    }

    [Fact(DisplayName = "Concede quando o token traz a permissão no client da API")]
    public async Task Decide_ComPapelNoClientDaApi_Concede()
    {
        AuthorizationDecision decisao = await DecidirComPapeis(Permissao);

        decisao.Allowed.Should().BeTrue();
        decisao.GrantUsed!.Fonte.Should().Be(FonteGrant.Token);
    }

    [Fact(DisplayName = "Concede a consulta auditável quando o token traz essa permissão")]
    public async Task Decide_ComPapelDeConsultaAuditavel_Concede()
    {
        AuthorizationDecision decisao = await DecidirComPapeis(
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement,
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria);

        decisao.Allowed.Should().BeTrue(
            "a consulta é concedida pelo mesmo caminho da manutenção — um papel do client da API, "
            + "atribuível a qualquer perfil institucional sem mudança de código");
        decisao.GrantUsed!.Fonte.Should().Be(FonteGrant.Token);
    }

    [Fact(DisplayName = "O papel de manutenção não concede a consulta auditável no host")]
    public async Task Decide_ComPapelDeManutencao_NaoConcedeConsultaAuditavel()
    {
        AuthorizationDecision decisao = await DecidirComPapeis(
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement,
            Permissao);

        decisao.Allowed.Should().BeFalse(
            "escrita e leitura protegida são concessões distintas — uma não implica a outra");
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Nega quando o token não traz a permissão")]
    public async Task Decide_SemPapel_Nega()
    {
        AuthorizationDecision decisao = await DecidirComPapeis("configuracao:outra-coisa:manter");

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Papéis aninhados sob a audiência não concedem — a chave é o clientId")]
    public async Task Decide_PapeisSobAAudiencia_Nega()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        AuthOptions auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        auth.ClientId.Should().NotBe(auth.Audience,
            "a ADR-0010 mantém o clientId (uniplus-api) distinto da audiência (uniplus)");

        Result<AuthorizationSubject> sujeito = await ResolverSujeito(
            scope,
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            new Claim("resource_access", JsonSerializer.Serialize(
                new Dictionary<string, object> { [auth.Audience] = new { roles = new[] { Permissao } } })));

        AuthorizationDecision decisao = await scope.ServiceProvider
            .GetRequiredService<IAuthorizationDecisionService>()
            .DecideAsync(
                sujeito.Value!,
                UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement,
                ResourceContext.From("MotivoDecisaoRecursal", Sensibilidade.Interna).Value!,
                Requisicao(),
                CancellationToken.None);

        decisao.Allowed.Should().BeFalse(
            "montar o token com os papéis sob a audiência era o que mascarava a leitura da chave errada");
    }

    [Fact(DisplayName = "Token sem jti dá identidade incompleta, não negativa de acesso")]
    public async Task Verificar_TokenSemJti_DaIdentidadeIncompleta()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        AuthOptions auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("iss", Emissor),
                        new Claim("sub", "sub-opaco-1"),
                        new Claim("resource_access", JsonSerializer.Serialize(
                            new Dictionary<string, object> { [auth.ClientId] = new { roles = new[] { Permissao } } })),
                    ],
                    authenticationType: "Bearer")),
            };

        ResultadoDoAcesso resultado = await scope.ServiceProvider
            .GetRequiredService<IVerificadorDeAcesso>()
            .VerificarAsync(UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement);

        resultado.Should().Be(ResultadoDoAcesso.IdentidadeIncompleta,
            "responder como negativa faria a borda devolver 403 a quem sequer pôde ser identificado");
    }

    [Fact(DisplayName = "Token sem jti não produz sujeito — a falha é de autenticação, antes da decisão")]
    public async Task Resolve_TokenSemJti_Falha()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();

        Result<AuthorizationSubject> sujeito = await ResolverSujeito(
            scope,
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"));

        sujeito.IsFailure.Should().BeTrue();
    }

    private Task<AuthorizationDecision> DecidirComPapeis(params string[] papeis) =>
        DecidirComPapeis(
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement,
            papeis);

    private async Task<AuthorizationDecision> DecidirComPapeis(
        PermissionRequirement requisito,
        params string[] papeis)
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();

        // O client sob o qual o token aninha os papéis desta API é o clientId
        // (uniplus-api), NÃO a audiência (uniplus) — a ADR-0010 separa os dois e
        // lista a confusão entre eles como risco. Ler do próprio contêiner amarra
        // o teste ao valor real do host.
        AuthOptions auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        Result<AuthorizationSubject> sujeito = await ResolverSujeito(
            scope,
            new Claim("iss", Emissor),
            new Claim("sub", "sub-opaco-1"),
            new Claim("jti", "jti-1"),
            new Claim("resource_access", JsonSerializer.Serialize(
                new Dictionary<string, object> { [auth.ClientId] = new { roles = papeis } })));

        sujeito.IsSuccess.Should().BeTrue();

        return await scope.ServiceProvider
            .GetRequiredService<IAuthorizationDecisionService>()
            .DecideAsync(
                sujeito.Value!,
                requisito,
                ResourceContext.From("MotivoDecisaoRecursal", Sensibilidade.Interna).Value!,
                Requisicao(),
                CancellationToken.None);
    }

    private static Task<Result<AuthorizationSubject>> ResolverSujeito(
        IServiceScope scope,
        params Claim[] claims)
    {
        // O contexto do usuário é lido do acessor no momento em que é construído,
        // então o principal precisa estar no lugar antes de resolvê-lo.
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
            };

        return scope.ServiceProvider
            .GetRequiredService<IAuthorizationSubjectResolver>()
            .ResolveAsync(
                scope.ServiceProvider.GetRequiredService<IUserContext>(),
                Requisicao(),
                CancellationToken.None);
    }

    private static AuthorizationRequestContext Requisicao()
        => AuthorizationRequestContext.From(
            "req-host-1244",
            new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.Zero),
            OrigemRequisicao.Api).Value!;
}
