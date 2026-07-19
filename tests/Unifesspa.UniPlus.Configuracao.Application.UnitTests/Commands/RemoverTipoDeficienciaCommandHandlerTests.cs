namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverTipoDeficienciaCommandHandlerTests
{
    private readonly ITipoDeficienciaRepository _repository = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoDeficiencia Tipo() =>
        TipoDeficiencia.Criar("Física", "Deficiência física").Value!;

    [Fact(DisplayName = "Remover um tipo existente faz soft-delete (Remover + Salvar) sem bloqueio")]
    public async Task Handle_TipoExistente_FazSoftDelete()
    {
        TipoDeficiencia tipo = Tipo();
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result resultado = await RemoverTipoDeficienciaCommandHandler.Handle(
            new RemoverTipoDeficienciaCommand(tipo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(tipo);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remover um tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDeficiencia?)null);

        Result resultado = await RemoverTipoDeficienciaCommandHandler.Handle(
            new RemoverTipoDeficienciaCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<TipoDeficiencia>());
    }
}
