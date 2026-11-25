namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ToCreatedResultTests
{
    private readonly ControllerBase _controlador = new ControllerDeTeste();
    private readonly IDomainErrorMapper _mapeador = new DomainErrorMappingRegistry(
        [new ErrosDaFronteiraHttpDomainErrorRegistration()],
        new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions
        {
            BaseUri = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/",
        })));

    [Fact]
    public async Task ToCreatedResult_ComSucesso_Devolve201ComEnderecoECorpo()
    {
        Guid id = Guid.NewGuid();
        CreatedAtActionResult resposta = Result<Guid>.Success(id)
            .ToCreatedResult(_controlador, "ObterPorId", _mapeador).Should().BeOfType<CreatedAtActionResult>().Subject;
        DefaultHttpContext contexto = await ExecutarAsync(resposta, $"/recursos/{id}");

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        contexto.Response.Headers.Location.ToString().Should().Be($"/recursos/{id}");
        resposta.ActionName.Should().Be("ObterPorId");
        resposta.RouteValues!["id"].Should().Be(id);
        resposta.Value.Should().Be(id);
    }

    [Fact]
    public void ToCreatedResult_ComFalha_DevolveStatusDoMapeador()
    {
        Result<Guid> resultado = Result<Guid>.Failure(new DomainError(
            ErrosDaFronteiraHttp.IdDaRotaDivergenteDoCorpo.Code, "Identificadores diferentes."));

        ObjectResult resposta = resultado.ToCreatedResult(_controlador, "ObterPorId", _mapeador)
            .Should().BeOfType<ObjectResult>().Subject;

        resposta.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        resposta.Value.Should().BeOfType<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("uniplus.contract.id_path_body_divergente");
    }

    private static async Task<DefaultHttpContext> ExecutarAsync(CreatedAtActionResult resposta, string endereco)
    {
        IUrlHelper url = Substitute.For<IUrlHelper>();
        url.Action(Arg.Any<UrlActionContext>()).Returns(endereco);
        resposta.UrlHelper = url;
        ServiceCollection servicos = new();
        servicos.AddLogging().AddMvc();
        using ServiceProvider provedor = servicos.BuildServiceProvider();
        DefaultHttpContext contexto = new() { RequestServices = provedor };
        await resposta.ExecuteResultAsync(new ActionContext { HttpContext = contexto });
        return contexto;
    }

    private sealed class ControllerDeTeste : ControllerBase;
}
