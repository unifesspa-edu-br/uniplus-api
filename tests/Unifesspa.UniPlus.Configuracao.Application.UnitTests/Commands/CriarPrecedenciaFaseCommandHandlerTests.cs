namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarPrecedenciaFaseCommandHandlerTests
{
    private readonly IPrecedenciaFaseRepository _repository = Substitute.For<IPrecedenciaFaseRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarPrecedenciaFaseCommand Comando() =>
        new("INSCRICAO", "HOMOLOGACAO");

    [Fact(DisplayName = "Grafo vazio cria a aresta, persiste e retorna o Id")]
    public async Task Handle_GrafoVazio_CriaEPersiste()
    {
        _repository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PrecedenciaFase>)[]);

        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<PrecedenciaFase>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Self-loop propaga o erro sem persistir")]
    public async Task Handle_SelfLoop_RetornaErroSemPersistir()
    {
        _repository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PrecedenciaFase>)[]);

        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            new CriarPrecedenciaFaseCommand("INSCRICAO", "INSCRICAO"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.SelfLoop);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<PrecedenciaFase>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Aresta duplicada no grafo vigente propaga o erro sem persistir")]
    public async Task Handle_ArestaDuplicada_RetornaErroSemPersistir()
    {
        PrecedenciaFase existente = PrecedenciaFase.Criar("INSCRICAO", "HOMOLOGACAO", false, []).Value!;
        _repository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PrecedenciaFase>)[existente]);

        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.ArestaDuplicada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Aresta que fecha ciclo no grafo vigente propaga o erro sem persistir")]
    public async Task Handle_Ciclo_RetornaErroSemPersistir()
    {
        PrecedenciaFase existente = PrecedenciaFase.Criar("INSCRICAO", "HOMOLOGACAO", false, []).Value!;
        _repository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PrecedenciaFase>)[existente]);

        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            new CriarPrecedenciaFaseCommand("HOMOLOGACAO", "INSCRICAO"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.CicloDetectado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
