namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

public sealed class PermissaoDoModeloNoRascunhoTests
{
    private readonly AuthorizationFilterContext _contexto;

    public PermissaoDoModeloNoRascunhoTests()
    {
        Guid processoId = Guid.CreateVersion7();
        IUnidadeDonaDoProcesso donos = Substitute.For<IUnidadeDonaDoProcesso>();
        donos.DoProcessoAsync(processoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Negado);
        ServiceCollection servicos = new();
        servicos.AddSingleton(donos);
        servicos.AddSingleton(verificador);
        servicos.AddSingleton(MapperDoModuloDeTeste.Criar());
        DefaultHttpContext http = new() { RequestServices = servicos.BuildServiceProvider() };
        RouteData rota = new() { Values = { ["processoSeletivoId"] = processoId } };
        _contexto = new(new ActionContext(http, rota, new ActionDescriptor()), []);
    }

    [Fact]
    public async Task Autorizar_ForaDaUnidade_RespondeNaoEncontrado()
    {
        await new ExigePermissaoNoProcessoAttribute(PermissaoSelecao.ProcessosConfigurar).OnAuthorizationAsync(_contexto);

        _contexto.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
