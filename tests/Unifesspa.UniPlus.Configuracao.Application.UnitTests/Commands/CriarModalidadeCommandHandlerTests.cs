namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarModalidadeCommand ComandoAmpla() =>
        new("AC", Descricao: "Ampla concorrência", NaturezaLegal: "AMPLA");

    [Fact(DisplayName = "Código livre cria a modalidade, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("AC", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            ComandoAmpla(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("AC", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            ComandoAmpla(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência de composição inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        _repository.CodigoExisteEntreVivosAsync("LB_PPI", null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // RETIRA_DE exige origem; a origem "XPTO" não existe viva.
        var comando = new CriarModalidadeCommand(
            "LB_PPI", NaturezaLegal: "AMPLA", ComposicaoVagas: "RETIRA_DE", ComposicaoOrigem: "XPTO");

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Invariante de domínio (cota sem cascata) propaga o erro sem persistir")]
    public async Task Handle_DominioIncoerente_RetornaErroSemPersistir()
    {
        _repository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);

        var comando = new CriarModalidadeCommand(
            "LB_PPI", NaturezaLegal: "COTA_RESERVADA", ComposicaoVagas: "DENTRO_DO_VR");

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
