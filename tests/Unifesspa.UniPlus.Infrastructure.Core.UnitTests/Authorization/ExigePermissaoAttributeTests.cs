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
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(desfecho);
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
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).VerificarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == Codigo),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ResourceTipo informado vira o resource da decisão, com a sensibilidade da permissão")]
    public async Task OnAuthorizationAsync_ComResourceTipo_InformaOResource()
    {
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = "catalogo" }
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).VerificarAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext?>(r => r!.ResourceTipo == "catalogo"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Negado na permissão principal, a alternativa em alguma unidade decide")]
    public async Task OnAuthorizationAsync_ComAlternativa_NegadoNaPrincipal_ConsultaAlgumaUnidade()
    {
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Negado);
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
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
        }
            .OnAuthorizationAsync(Contexto());

        await _acesso.DidNotReceive().VerificarEmAlgumaUnidadeAsync(
            Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>());
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

    private AuthorizationFilterContext Contexto()
    {
        ServiceProvider servicos = new ServiceCollection().AddSingleton(_acesso).BuildServiceProvider();
        DefaultHttpContext http = new() { RequestServices = servicos };

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }
}
