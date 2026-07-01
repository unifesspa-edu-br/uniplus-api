namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Modalidade Existente(string codigo = "AC") =>
        Modalidade.Criar(codigo, "Ampla", "AMPLA", "RESIDUAL_DO_VO", null, null, null, null, null, null, null, null)
            .Value!;

    [Fact(DisplayName = "Modalidade inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Modalidade?)null);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            new AtualizarModalidadeCommand(id, Descricao: "x"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        Modalidade existente = Existente("AC");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, Descricao: "Descrição nova", NaturezaLegal: "AMPLA", ComposicaoVagas: "RESIDUAL_DO_VO");

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("AC", "o código é imutável — não há campo para alterá-lo");
        existente.Descricao.Should().Be("Descrição nova");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência de remanejamento inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        Modalidade existente = Existente("SUP");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, NaturezaLegal: "SUPLEMENTAR", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            RegraRemanejamento: "DESTINO_UNICO", RemanejamentoDestino: "XPTO");

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
