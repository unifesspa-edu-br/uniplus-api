namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverOfertaCursoCommandHandlerTests
{
    private readonly IOfertaCursoRepository _repository = Substitute.For<IOfertaCursoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static OfertaCurso NovaOferta()
    {
        UnidadeOfertante unidade = UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

        return OfertaCurso.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), unidade, "REGULAR", null,
            null, null, null, null, null, null).Value!;
    }

    [Fact(DisplayName = "Oferta inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((OfertaCurso?)null);

        Result resultado = await RemoverOfertaCursoCommandHandler.Handle(
            new RemoverOfertaCursoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<OfertaCurso>());
    }

    [Fact(DisplayName = "Oferta existente é removida (soft-delete) sem bloqueio — snapshots externos são desacoplados (ADR-0061)")]
    public async Task Handle_Existente_RemoveSemBloqueio()
    {
        OfertaCurso oferta = NovaOferta();
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);

        Result resultado = await RemoverOfertaCursoCommandHandler.Handle(
            new RemoverOfertaCursoCommand(oferta.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(oferta);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
