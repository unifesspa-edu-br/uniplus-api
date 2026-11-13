namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Prova o ponto de decisão único montado pelo composition root real: os serviços saem do contêiner do host, o sujeito
/// se monta das concessões gravadas no Uni+ e a decisão sai do mesmo caminho que uma requisição percorreria. O token
/// identifica a pessoa e não concede acesso. As personas são as do portal: Leandro mantém os motivos de isenção, Betina
/// opera inscrições numa unidade e Diogo tem o papel mais amplo da plataforma sem concessão alguma.
/// </summary>
/// <remarks>
/// Os testes de unidade cobrem a regra; este cobre o que só o host responde: que <c>AddAutorizacao</c> e a infraestrutura
/// do módulo registram o que a decisão precisa, que a leitura das concessões chega à decisão pelo banco e que a decisão
/// registra a concessão usada.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PdpMinimoNoHostTests : IAsyncLifetime
{
    private const string Emissor = TestAuthHandler.DefaultIssuer;
    private const string Leandro = "leandro.viana";
    private const string Betina = "betina.neves";
    private const string Diogo = "diogo.souza";
    private const string ResourceTipo = "motivo-de-decisao";

    // Os requisitos são pessoais para que a decisão seja registrada: a permissão operacional decide sem gravar.
    private const string BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III";

    private static readonly PermissionRequirement ManterMotivos = Pessoal(PermissaoSelecao.MotivosDeDecisaoManter.Requisito);

    private static readonly PermissionRequirement OperarInscricoes =
        Pessoal(CatalogoDePermissoes<PermissaoDeTeste>.Obter(PermissaoDeTeste.InscricoesOperar).Requisito);

    private readonly MonolitoPostgresFixture _fixture;

    public PdpMinimoNoHostTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // As personas compartilham o banco da coleção: cada teste começa sem concessão alguma.
    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "O host resolve o ponto de decisão, o agregador do sujeito e a leitura das concessões")]
    public void Host_RegistraOsServicosDaDecisao()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<IAuthorizationDecisionService>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IAuthorizationSubjectResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IConcessoesAtivasDoSujeito>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IRegistroOperacionalRestrito>().Should().NotBeNull();

        // Resolver de fato, e não só consultar o registro: o verificador depende
        // de peças que outras extensões registram (contexto do usuário, acessor
        // de correlação, relógio). Uma delas ausente só apareceria na primeira
        // requisição que tentasse decidir.
        scope.ServiceProvider.Invoking(sp => sp.GetRequiredService<IVerificadorDeAcesso>())
            .Should().NotThrow();
    }

    [Fact(DisplayName = "Concede pela concessão institucional e o registro identifica a concessão, o perfil e a ausência de unidade")]
    public async Task Decide_ComConcessaoInstitucional_ConcedeERegistraAConcessao()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, ManterMotivos.Permissao);
        Guid concessao = await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);

        (AuthorizationDecision decisao, RegistroDecisaoAcesso registro) = await Decidir(Leandro, ManterMotivos, Resource());

        decisao.Allowed.Should().BeTrue();
        decisao.GrantUsed!.Fonte.Should().Be(FonteGrant.Concessao);
        registro.FonteGrant.Should().Be("concessao");
        registro.GrantId.Should().Be(concessao);
        registro.PerfilId.Should().Be(perfil);
        registro.VersaoId.Should().Be(decisao.GrantUsed.VersaoId).And.NotBeNull();
        registro.EscopoUnidadeId.Should().BeNull();
    }

    [Fact(DisplayName = "A concessão na unidade concede sobre o resource dela e é recusada sobre o de outra unidade")]
    public async Task Decide_ConcessaoDeUnidade_SoAlcancaAResourceDaUnidade()
    {
        Guid unidadeDoCeps = Guid.CreateVersion7();
        Guid outraUnidade = Guid.CreateVersion7();
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, OperarInscricoes.Permissao);
        Guid concessao = await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Betina, perfil, EscopoDaConcessao.DaUnidade(unidadeDoCeps));

        (AuthorizationDecision naUnidade, RegistroDecisaoAcesso registro) = await Decidir(Betina, OperarInscricoes, Resource(unidadeDoCeps));
        (AuthorizationDecision emOutra, _) = await Decidir(Betina, OperarInscricoes, Resource(outraUnidade));

        naUnidade.Allowed.Should().BeTrue();
        registro.GrantId.Should().Be(concessao);
        registro.PerfilId.Should().Be(perfil);
        registro.EscopoUnidadeId.Should().Be(unidadeDoCeps);
        emOutra.Allowed.Should().BeFalse();
        emOutra.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "A concessão institucional não alcança resource de uma unidade")]
    public async Task Decide_ConcessaoInstitucional_NaoAlcancaResourceDeUnidade()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, ManterMotivos.Permissao);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);

        (AuthorizationDecision decisao, _) = await Decidir(Leandro, ManterMotivos, Resource(Guid.CreateVersion7()));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Papel do client da API e papel de realm no token não concedem, ainda que sejam o código da permissão")]
    public async Task Decide_PapelNoToken_NaoConcede()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        AuthOptions auth = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;

        Result<AuthorizationSubject> sujeito = await ResolverSujeito(
            scope, [new Claim("iss", Emissor), new Claim("sub", "pessoa-so-com-papel"), new Claim("jti", "jti-1"), .. PapeisComOCodigoDaPermissao(auth.ClientId)]);

        sujeito.Value!.ConcessoesEfetivas.Should().BeEmpty();
        AuthorizationDecision decisao = await DecidirComServico(scope, sujeito.Value, ManterMotivos, Resource(), Agora());
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão vencida é recusada como expirada, para orientar a renovação")]
    public async Task Decide_ConcessaoVencida_NegaPorConcessaoExpirada()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, ManterMotivos.Permissao);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, validaAte: DateTimeOffset.UtcNow.AddHours(1));

        (AuthorizationDecision decisao, _) = await Decidir(
            Leandro, ManterMotivos, Resource(), dataAcesso: DateTimeOffset.UtcNow.AddDays(2));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Perfil desativado continua concedendo: retirar acesso é revogar a concessão")]
    public async Task Decide_PerfilDesativado_ContinuaConcedendo()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, ManterMotivos.Permissao);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
                "UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", perfil);
        }

        (AuthorizationDecision decisao, _) = await Decidir(Leandro, ManterMotivos, Resource());

        decisao.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "A concessão revogada deixa de valer na decisão seguinte")]
    public async Task Decide_ConcessaoRevogada_NegaNaSeguinte()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, ManterMotivos.Permissao);
        Guid concessao = await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);
        (await Decidir(Leandro, ManterMotivos, Resource())).Decisao.Allowed.Should().BeTrue();

        await RevogarAsync(concessao);

        (AuthorizationDecision depois, _) = await Decidir(Leandro, ManterMotivos, Resource());

        depois.Allowed.Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura das concessões é ordenada pela data da concessão e pelo identificador, para o desempate ser estável")]
    public async Task Listar_Concessoes_OrdemDeterministica()
    {
        for (int i = 0; i < 3; i++)
        {
            await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, ManterMotivos.Permissao);
        }

        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        IReadOnlyList<ConcessaoAtiva> lidas = await scope.ServiceProvider
            .GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(Emissor, Leandro).Value!, CancellationToken.None);
        Guid[] esperada = [.. (await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
            .Where(c => c.BeneficiarioSub == Leandro).ToListAsync())
            .OrderBy(c => c.ConcedidaEm).ThenBy(c => c.Id).Select(c => c.Id)];

        lidas.Select(c => c.ConcessaoId).Should().Equal(esperada);
    }

    [Fact(DisplayName = "Token sem jti dá identidade incompleta, não negativa de acesso")]
    public async Task Verificar_TokenSemJti_DaIdentidadeIncompleta()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        DefinirPrincipal(scope, new Claim("iss", Emissor), new Claim("sub", "sub-opaco-1"));

        ResultadoDoAcesso resultado = await scope.ServiceProvider
            .GetRequiredService<IVerificadorDeAcesso>()
            .ExigirAsync(ManterMotivos);

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

    // A decisão sai do serviço real, e o registro é o que o verificador gravaria para ela. Cada decisão abre o próprio escopo, como cada requisição.
    private async Task<(AuthorizationDecision Decisao, RegistroDecisaoAcesso Registro)> Decidir(
        string sub,
        PermissionRequirement requisito,
        ResourceContext resource,
        DateTimeOffset? dataAcesso = null)
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        Result<AuthorizationSubject> sujeito = await ResolverSujeito(
            scope, new Claim("iss", Emissor), new Claim("sub", sub), new Claim("jti", "jti-1"));
        sujeito.IsSuccess.Should().BeTrue();

        DateTimeOffset instante = dataAcesso ?? Agora();
        AuthorizationDecision decisao = await DecidirComServico(scope, sujeito.Value!, requisito, resource, instante);

        return (decisao, RegistroDecisaoAcesso.De(sujeito.Value!, requisito, resource, Requisicao(instante), decisao));
    }

    private static Task<AuthorizationDecision> DecidirComServico(
        IServiceScope scope,
        AuthorizationSubject sujeito,
        PermissionRequirement requisito,
        ResourceContext resource,
        DateTimeOffset dataAcesso) =>
        new AuthorizationDecisionService(
            scope.ServiceProvider.GetRequiredService<GrantEfetivoAplicavelCheck>(),
            scope.ServiceProvider.GetRequiredService<ImpedimentoPorInscricaoCheck>(),
            scope.ServiceProvider.GetServices<IPermissaoCheck>())
        .DecidirAsync(sujeito, requisito, resource, Requisicao(dataAcesso), CancellationToken.None);

    private static Task<Result<AuthorizationSubject>> ResolverSujeito(
        IServiceScope scope,
        params Claim[] claims)
    {
        // O contexto do usuário é lido do acessor no momento em que é construído,
        // então o principal precisa estar no lugar antes de resolvê-lo.
        DefinirPrincipal(scope, claims);

        return scope.ServiceProvider
            .GetRequiredService<IAuthorizationSubjectResolver>()
            .ResolveAsync(
                scope.ServiceProvider.GetRequiredService<IUserContext>(),
                Requisicao(Agora()),
                CancellationToken.None);
    }

    private static void DefinirPrincipal(IServiceScope scope, params Claim[] claims) =>
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
            };

    // O papel do client da API e o de realm trazem o próprio código da permissão e o papel mais amplo da plataforma.
    private static Claim[] PapeisComOCodigoDaPermissao(string clientId)
    {
        string[] papeis = [ManterMotivos.Permissao, "plataforma-admin"];

        return
        [
            new Claim("resource_access", JsonSerializer.Serialize(new Dictionary<string, object> { [clientId] = new { roles = papeis } })),
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = papeis })),
        ];
    }

    private async Task RevogarAsync(Guid concessaoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        Concessao gravada = await db.Concessoes.SingleAsync(c => c.Id == concessaoId);
        AtoDoAgente revogacao = new(new Identidade(Emissor, Diogo), DateTimeOffset.UtcNow);
        gravada.Revogar(revogacao, "Mudou de função na unidade.").IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private static PermissionRequirement Pessoal(PermissionRequirement original) =>
        PermissionRequirement.From(
            original.Permissao,
            ClassificacaoDeProtecao.Pessoal,
            BaseLegal,
            original.RequerMfa,
            original.RequerDuplaAprovacao,
            original.EscopoContextoObrigatorio,
            original.VerificacoesDeContexto).Value!;

    private static ResourceContext Resource(Guid? unidadeProprietariaId = null) =>
        ResourceContext.From(ResourceTipo, ClassificacaoDeProtecao.Interno, unidadeProprietariaId).Value!;

    private static DateTimeOffset Agora() => DateTimeOffset.UtcNow;

    private static AuthorizationRequestContext Requisicao(DateTimeOffset dataAcesso)
        => AuthorizationRequestContext.From("req-host-612", dataAcesso, OrigemRequisicao.Api).Value!;
}
