namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CampusResponsavelDoLocalTests
{
    private readonly ICampusRepository _campusRepository = Substitute.For<ICampusRepository>();

    [Fact(DisplayName = "Campus existente e vivo é aceito")]
    public async Task ConferirAsync_CampusVivo_Aceita()
    {
        Guid campusId = Guid.CreateVersion7();
        _campusRepository.ExisteVivoAsync(campusId, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await CampusResponsavelDoLocal.ConferirAsync(campusId, _campusRepository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Campus inexistente ou removido é recusado com o código e a mensagem do campus responsável")]
    public async Task ConferirAsync_CampusInexistenteOuRemovido_Recusa()
    {
        Guid campusId = Guid.CreateVersion7();
        _campusRepository.ExisteVivoAsync(campusId, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await CampusResponsavelDoLocal.ConferirAsync(campusId, _campusRepository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado);
        resultado.Error.Message.Should().Be("O Campus responsável informado não foi encontrado.");
    }

    [Fact(DisplayName = "A conferência consulta o campus informado, e só ele")]
    public async Task ConferirAsync_ConsultaOCampusInformado()
    {
        Guid campusId = Guid.CreateVersion7();

        await CampusResponsavelDoLocal.ConferirAsync(campusId, _campusRepository, CancellationToken.None);

        await _campusRepository.Received(1).ExisteVivoAsync(campusId, Arg.Any<CancellationToken>());
        await _campusRepository.Received(1).ExisteVivoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
