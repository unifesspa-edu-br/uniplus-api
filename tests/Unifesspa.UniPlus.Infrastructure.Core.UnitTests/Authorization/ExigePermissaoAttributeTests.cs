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

    [Fact(DisplayName = "RecursoTipo informado vira o recurso da decisão, com a sensibilidade da permissão")]
    public async Task OnAuthorizationAsync_ComRecursoTipo_InformaORecurso()
    {
        _acesso.VerificarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo) { RecursoTipo = "Catalogo" }
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).VerificarAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext?>(r => r!.RecursoTipo == "Catalogo"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Permissão de outro enum não constrói o atributo")]
    public void Construtor_PermissaoDeOutroEnum_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new(DayOfWeek.Monday);

        criar.Should().Throw<ArgumentException>().WithMessage("*PermissaoAutorizacao*");
    }

    [Fact(DisplayName = "RecursoTipo em branco falha ao construir o atributo, não na requisição")]
    public void RecursoTipo_EmBranco_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar =
            () => new(PermissaoAutorizacao.ConsultarCatalogo) { RecursoTipo = " " };

        criar.Should().Throw<ArgumentException>();
    }

    private AuthorizationFilterContext Contexto()
    {
        ServiceProvider servicos = new ServiceCollection().AddSingleton(_acesso).BuildServiceProvider();
        DefaultHttpContext http = new() { RequestServices = servicos };

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }
}
