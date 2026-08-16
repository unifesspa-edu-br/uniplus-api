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

    /// <summary>
    /// Self-loop não depende do grafo vigente (só compara os dois códigos) — o
    /// handler valida via <see cref="PrecedenciaFase.ValidarCodigos"/> antes de
    /// travar o grafo, então self-loop nunca chega a travar nem a consultar.
    /// </summary>
    [Fact(DisplayName = "Self-loop propaga o erro sem travar o grafo, sem consultar arestas vivas e sem persistir")]
    public async Task Handle_SelfLoop_RetornaErroSemTravarNemPersistir()
    {
        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            new CriarPrecedenciaFaseCommand("INSCRICAO", "INSCRICAO"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.SelfLoop);
        await _repository.DidNotReceive().TravarGrafoParaEscritaAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<PrecedenciaFase>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Mesmo raciocínio do self-loop: código em formato inválido é rejeitado sem
    /// I/O nenhum — um payload já inválido nunca deveria travar o grafo alheio.
    /// </summary>
    [Fact(DisplayName = "Código em formato inválido propaga a violação de campo sem travar o grafo nem consultar arestas vivas")]
    public async Task Handle_CodigoFormatoInvalido_RetornaViolacaoDeCampoSemTravarGrafo()
    {
        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            new CriarPrecedenciaFaseCommand("inscricao", "HOMOLOGACAO"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.AntecessoraCodigoFormatoInvalido);
        await _repository.DidNotReceive().TravarGrafoParaEscritaAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Antecessora e sucessora ausentes ao mesmo tempo acumulam as duas violações sem travar o grafo")]
    public async Task Handle_AmbosOsCodigosAusentes_AcumulaAsDuasViolacoesSemTravarGrafo()
    {
        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            new CriarPrecedenciaFaseCommand(null, null), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("antecessoraCodigo");
        resultado.Errors[1].Field.Should().Be("sucessoraCodigo");
        await _repository.DidNotReceive().TravarGrafoParaEscritaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Entrada válida trava o grafo antes de consultar as arestas vivas")]
    public async Task Handle_EntradaValida_TravaGrafoAntesDeConsultarArestasVivas()
    {
        _repository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PrecedenciaFase>)[]);

        Result<Guid> resultado = await CriarPrecedenciaFaseCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _repository.TravarGrafoParaEscritaAsync(Arg.Any<CancellationToken>());
            _repository.ListarVivasAsync(Arg.Any<CancellationToken>());
        });
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
