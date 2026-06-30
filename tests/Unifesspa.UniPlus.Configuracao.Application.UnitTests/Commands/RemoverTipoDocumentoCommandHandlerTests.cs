namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverTipoDocumentoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _repository = Substitute.For<ITipoDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoDocumento Tipo() =>
        TipoDocumento.Criar("RG", "Registro Geral", null, "IDENTIFICACAO", null, null, null).Value!;

    [Fact(DisplayName = "Remover um tipo existente faz soft-delete (Remover + Salvar) sem bloqueio")]
    public async Task Handle_TipoExistente_FazSoftDelete()
    {
        TipoDocumento tipo = Tipo();
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result resultado = await RemoverTipoDocumentoCommandHandler.Handle(
            new RemoverTipoDocumentoCommand(tipo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(tipo);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remover um tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDocumento?)null);

        Result resultado = await RemoverTipoDocumentoCommandHandler.Handle(
            new RemoverTipoDocumentoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<TipoDocumento>());
    }
}
