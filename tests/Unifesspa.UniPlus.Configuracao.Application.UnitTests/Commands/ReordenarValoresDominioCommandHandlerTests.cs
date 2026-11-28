namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ReordenarValoresDominioCommandHandlerTests
{
    private readonly IFatoCandidatoRepository _repository = Substitute.For<IFatoCandidatoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "A lista de códigos ausente em fato inexistente é recusada com 422, antes da busca e do 404")]
    public async Task Handle_CodigosAusentesEFatoInexistente_Recusa422SemBuscar()
    {
        Result resultado = await ReordenarValoresDominioCommandHandler.Handle(
            new ReordenarValoresDominioCommand(Guid.CreateVersion7(), null), _repository, _unitOfWork, CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        (recusa.Field, recusa.Error.Code).Should().Be(("codigos", CodigosDeValidacao.Obrigatorio));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista de códigos informada em fato inexistente segue devolvendo 404")]
    public async Task Handle_CodigosInformadosEFatoInexistente_RetornaNaoEncontrado()
    {
        Result resultado = await ReordenarValoresDominioCommandHandler.Handle(
            new ReordenarValoresDominioCommand(Guid.CreateVersion7(), []), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaoEncontrado);
    }
}
