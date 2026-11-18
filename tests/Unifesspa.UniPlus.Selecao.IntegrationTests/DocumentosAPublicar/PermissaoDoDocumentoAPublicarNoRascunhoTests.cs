namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Reflection;

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
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

/// <summary>
/// O filtro que protege as rotas do documento a publicar, configurado como o controller o declara, decide pela unidade do
/// rascunho enquanto o processo não tem linha: a permissão de publicar vale na unidade dona, e quem não a tem recebe a
/// resposta de processo inexistente.
/// </summary>
public sealed class PermissaoDoDocumentoAPublicarNoRascunhoTests
{
    private readonly Guid _rascunhoId = Guid.CreateVersion7();
    private readonly Guid _unidadeDoRascunho = Guid.CreateVersion7();
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private readonly IUnidadeDonaDoProcesso _donos = Substitute.For<IUnidadeDonaDoProcesso>();

    private async Task<AuthorizationFilterContext> FiltrarAsync()
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton(_verificador);
        servicos.AddSingleton(_donos);
        servicos.AddSingleton(MapperDoModuloDeTeste.Criar());
        RouteData rota = new() { Values = { ["processoSeletivoId"] = _rascunhoId } };
        DefaultHttpContext http = new() { RequestServices = servicos.BuildServiceProvider() };
        AuthorizationFilterContext contexto = new(new ActionContext(http, rota, new ActionDescriptor()), []);

        await AtributoDoController().OnAuthorizationAsync(contexto);
        return contexto;
    }

    private static ExigePermissaoNoProcessoAttribute AtributoDoController() =>
        typeof(DocumentosAPublicarController).GetCustomAttribute<ExigePermissaoNoProcessoAttribute>()!;

    [Fact(DisplayName = "Com a permissão de publicar na unidade do rascunho, a requisição segue")]
    public async Task ComPermissaoNaUnidadeDoRascunho_Segue()
    {
        _donos.DoProcessoAsync(_rascunhoId, Arg.Any<CancellationToken>()).Returns(_unidadeDoRascunho);
        _verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Permitido);

        AuthorizationFilterContext contexto = await FiltrarAsync();

        contexto.Result.Should().BeNull();
        await _verificador.Received(1).ExigirAsync(
            PermissaoSelecao.ProcessosPublicar.Requisito,
            Arg.Is<ResourceContext?>(r => r!.UnidadeProprietariaId == _unidadeDoRascunho && r.ProcessoId == _rascunhoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem a permissão na unidade do rascunho, a resposta é a de processo inexistente")]
    public async Task SemPermissaoNaUnidadeDoRascunho_ProcessoInexistente()
    {
        _donos.DoProcessoAsync(_rascunhoId, Arg.Any<CancellationToken>()).Returns(_unidadeDoRascunho);
        _verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Negado);

        AuthorizationFilterContext contexto = await FiltrarAsync();

        contexto.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "Sem rascunho nem processo, a resposta é a de processo inexistente sem consultar o ponto de decisão")]
    public async Task SemRascunhoNemProcesso_ProcessoInexistente()
    {
        AuthorizationFilterContext contexto = await FiltrarAsync();

        contexto.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }
}
