namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ConferirIdDaRotaTests
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";
    private const string CodigoDaFronteira = "uniplus.contract.id_path_body_divergente";

    private readonly ControllerBase _controller = new ControllerDeTeste();
    private readonly IDomainErrorMapper _mapper = new DomainErrorMappingRegistry(
        [new ErrosDaFronteiraHttpDomainErrorRegistration()],
        new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo })));

    [Fact(DisplayName = "Ids iguais não geram recusa")]
    public void ConferirIdDaRota_IdsIguais_DevolveNulo()
    {
        Guid id = Guid.NewGuid();

        _controller.ConferirIdDaRota(id, id, _mapper).Should().BeNull();
    }

    [Fact(DisplayName = "Guid vazio na rota e no corpo conta como igual")]
    public void ConferirIdDaRota_GuidVazioNosDois_DevolveNulo()
    {
        _controller.ConferirIdDaRota(Guid.Empty, Guid.Empty, _mapper).Should().BeNull();
    }

    [Fact(DisplayName = "Ids diferentes geram o 400 registrado na fronteira, com type, title, detail e traceId")]
    public void ConferirIdDaRota_IdsDiferentes_Devolve400DaFronteira()
    {
        IActionResult? recusa = _controller.ConferirIdDaRota(Guid.NewGuid(), Guid.NewGuid(), _mapper);

        ObjectResult resposta = recusa.Should().BeOfType<ObjectResult>().Subject;
        resposta.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ProblemDetails problem = resposta.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(CodigoDaFronteira);
        problem.Type.Should().Be(BaseUriDoCatalogo + CodigoDaFronteira);
        problem.Title.Should().Be("Id da rota diferente do Id do corpo");
        problem.Detail.Should().Be(
            "O Id informado na rota é diferente do Id do corpo da requisição; os dois precisam identificar o mesmo recurso.");
        problem.Extensions["traceId"]?.ToString().Should().NotBeNullOrEmpty();
    }

    private sealed class ControllerDeTeste : ControllerBase;
}
