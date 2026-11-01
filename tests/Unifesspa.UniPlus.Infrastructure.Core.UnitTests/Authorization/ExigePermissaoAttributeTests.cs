namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class ExigePermissaoAttributeTests
{
    private const string Codigo = "autorizacao:permissoes:consultar-catalogo";

    private readonly IVerificadorDeAcesso _acesso = Substitute.For<IVerificadorDeAcesso>();

    [Theory(DisplayName = "Traduz os três desfechos da verificação para a resposta da borda")]
    [InlineData(ResultadoDoAcesso.Permitido, null)]
    [InlineData(ResultadoDoAcesso.Negado, typeof(ForbidResult))]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta, typeof(ChallengeResult))]
    public async Task OnAuthorizationAsync_TraduzODesfecho(ResultadoDoAcesso desfecho, Type? resposta)
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(desfecho));
        AuthorizationFilterContext contexto = Contexto();

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
            .OnAuthorizationAsync(contexto);

        if (resposta is null)
        {
            contexto.Result.Should().BeNull();
        }
        else
        {
            contexto.Result.Should().BeOfType(resposta);
        }
    }

    [Fact(DisplayName = "Pede ao verificador o requisito da permissão do catálogo")]
    public async Task OnAuthorizationAsync_PedeORequisitoDoCatalogo()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).DecidirSemRegistrarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == Codigo),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ResourceTipo informado vira o resource da decisão, com a sensibilidade da permissão")]
    public async Task OnAuthorizationAsync_ComResourceTipo_InformaOResource()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = "catalogo" }
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).DecidirSemRegistrarAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext?>(r => r!.ResourceTipo == "catalogo"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Negado na permissão principal, a alternativa em alguma unidade decide")]
    public async Task OnAuthorizationAsync_ComAlternativa_NegadoNaPrincipal_ConsultaAlgumaUnidade()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Negado));
        _acesso.VerificarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);
        AuthorizationFilterContext contexto = Contexto();

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
        }
            .OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeNull();
        await _acesso.Received(1).VerificarEmAlgumaUnidadeAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "autorizacao:perfis:manter"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Permitido na principal, a alternativa nem é consultada")]
    public async Task OnAuthorizationAsync_ComAlternativa_PermitidoNaPrincipal_NaoConsultaAlgumaUnidade()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
        }
            .OnAuthorizationAsync(Contexto());

        await _acesso.DidNotReceive().VerificarEmAlgumaUnidadeAsync(
            Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Alternativa permitida: a negativa da principal não é registrada")]
    public async Task OnAuthorizationAsync_AlternativaPermite_NaoRegistraANegativaDaPrincipal()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar));
        _acesso.VerificarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
        }
            .OnAuthorizationAsync(Contexto());

        await _acesso.DidNotReceiveWithAnyArgs().RegistrarAsync(default(IReadOnlyCollection<DecisaoDeAcesso>)!, default);
    }

    [Fact(DisplayName = "Principal e alternativa negadas: a negativa da principal é registrada")]
    public async Task OnAuthorizationAsync_AmbasNegam_RegistraANegativaDaPrincipal()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar));
        _acesso.VerificarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Negado);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
        }
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).RegistrarAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 1 && d.Single().Resultado == ResultadoDoAcesso.Negado),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Permissão operacional não registra, permitida ou negada")]
    public async Task OnAuthorizationAsync_PermissaoOperacional_NaoRegistra()
    {
        _acesso.DecidirSemRegistrarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(Decisao(ResultadoDoAcesso.Negado));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo).OnAuthorizationAsync(Contexto());

        await _acesso.DidNotReceiveWithAnyArgs().RegistrarAsync(default(IReadOnlyCollection<DecisaoDeAcesso>)!, default);
    }

    [Fact(DisplayName = "Permissão de outro enum não constrói o atributo")]
    public void Construtor_PermissaoDeOutroEnum_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new(DayOfWeek.Monday);

        criar.Should().Throw<ArgumentException>().WithMessage("*PermissaoAutorizacao*");
    }

    [Fact(DisplayName = "ResourceTipo em branco falha ao construir o atributo, não na requisição")]
    public void ResourceTipo_EmBranco_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar =
            () => new(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = " " };

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ResourceTipo fora da convenção falha ao construir o atributo, não na requisição")]
    public void ResourceTipo_ForaDaConvencao_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar =
            () => new(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = "TiposDocumento" };

        criar.Should().Throw<ArgumentException>().WithMessage("*kebab-case*");
    }

    private static DecisaoDeAcesso Decisao(
        ResultadoDoAcesso resultado, PermissaoAutorizacao permissao = PermissaoAutorizacao.ConsultarCatalogo)
    {
        if (resultado == ResultadoDoAcesso.IdentidadeIncompleta)
        {
            return DecisaoDeAcesso.IdentidadeIncompleta;
        }

        PermissionRequirement requisito = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "diogo.souza").Value!, "jti-teste").Value!;
        AuthorizationDecision decisao = resultado == ResultadoDoAcesso.Permitido
            ? AuthorizationDecision.Permitir(EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!)
            : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);

        return DecisaoDeAcesso.Tomada(
            resultado,
            RegistroDecisaoAcesso.De(
                sujeito,
                requisito,
                ResourceContext.From(ResourceTipos.Operacao, requisito.Sensibilidade).Value!,
                AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!,
                decisao));
    }

    private AuthorizationFilterContext Contexto()
    {
        ServiceProvider servicos = new ServiceCollection().AddSingleton(_acesso).BuildServiceProvider();
        DefaultHttpContext http = new() { RequestServices = servicos };

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }
}
