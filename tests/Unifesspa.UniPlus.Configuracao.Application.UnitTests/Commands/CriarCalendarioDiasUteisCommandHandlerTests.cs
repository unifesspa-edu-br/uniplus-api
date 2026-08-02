namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCalendarioDiasUteisCommandHandlerTests
{
    private readonly ICalendarioDiasUteisRepository _repository = Substitute.For<ICalendarioDiasUteisRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarCalendarioDiasUteisCommand ComandoValido() =>
        new(
            "2027.1",
            [new DiaNaoUtilCommandItem("NACIONAL", null, new DateOnly(2027, 1, 1), "Confraternização Universal")]);

    [Fact(DisplayName = "Cria o calendário, persiste e retorna o Id")]
    public async Task Handle_ComandoValido_CriaEPersiste()
    {
        Result<Guid> resultado = await CriarCalendarioDiasUteisCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<CalendarioDiasUteis>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Comando com versão do dataset vazia falha sem persistir")]
    public async Task Handle_VersaoDatasetVazia_RetornaErroSemPersistir()
    {
        CriarCalendarioDiasUteisCommand comando = ComandoValido() with { VersaoDataset = string.Empty };

        Result<Guid> resultado = await CriarCalendarioDiasUteisCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<CalendarioDiasUteis>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
