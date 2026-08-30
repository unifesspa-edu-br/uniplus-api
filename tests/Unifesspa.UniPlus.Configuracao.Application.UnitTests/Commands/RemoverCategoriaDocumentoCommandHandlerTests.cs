namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverCategoriaDocumentoCommandHandlerTests
{
    private readonly ICategoriaDocumentoRepository _repository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "Categoria existente é removida e a remoção é persistida")]
    public async Task Handle_Existente_RemoveEPersiste()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar("OBSOLETA", "Obsoleta", null, 0).Value!;
        _repository.ObterPorIdAsync(categoria.Id, Arg.Any<CancellationToken>()).Returns(categoria);

        Result resultado = await RemoverCategoriaDocumentoCommandHandler.Handle(
            new RemoverCategoriaDocumentoCommand(categoria.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(categoria);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Id inexistente retorna NaoEncontrada sem remover nem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CategoriaDocumento?)null);

        Result resultado = await RemoverCategoriaDocumentoCommandHandler.Handle(
            new RemoverCategoriaDocumentoCommand(Guid.CreateVersion7()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<CategoriaDocumento>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
