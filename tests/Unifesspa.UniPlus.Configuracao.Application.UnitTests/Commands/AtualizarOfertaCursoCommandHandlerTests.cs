namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarOfertaCursoCommandHandlerTests
{
    private static readonly Guid CursoId = Guid.CreateVersion7();
    private static readonly Guid LocalOfertaId = Guid.CreateVersion7();

    private readonly IOfertaCursoRepository _repository = Substitute.For<IOfertaCursoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static UnidadeOfertante Unidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    private static OfertaCurso OfertaExistente(UnidadeOfertante? unidade = null) =>
        OfertaCurso.Criar(
            CursoId, LocalOfertaId, unidade ?? Unidade(), "REGULAR", "PRESENCIAL",
            "MATUTINO", "123456", "ENG-01", 40, null, null).Value!;

    private static AtualizarOfertaCursoCommand Comando(
        Guid id,
        string programa = "REGULAR",
        string? baseLegal = null) =>
        new(id, programa, FormatoPedagogico: "EAD", Turno: null,
            EMecCodigo: "654321", CodigoSga: "ENG-02",
            VagasAnuaisAutorizadas: 60, BaseLegal: baseLegal, AtoAutorizacaoMec: null);

    [Fact(DisplayName = "Oferta inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((OfertaCurso?)null);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida troca os atributos regulatórios e persiste")]
    public async Task Handle_AtualizacaoValida_Persiste()
    {
        OfertaCurso oferta = OfertaExistente();
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(oferta.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        oferta.FormatoPedagogico.Should().Be(FormatoPedagogico.Ead);
        oferta.Turno.Should().BeNull();
        oferta.EMecCodigo.Should().Be("654321");
        oferta.CodigoSga.Should().Be("ENG-02");
        oferta.VagasAnuaisAutorizadas.Should().Be(60);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Transição Regular→Parfor sem base legal é rejeitada sem persistir (guard revalidado)")]
    public async Task Handle_TransicaoRegularParforSemBaseLegal_RejeitaSemPersistir()
    {
        OfertaCurso oferta = OfertaExistente();
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(oferta.Id, programa: "PARFOR", baseLegal: null),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
        oferta.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Regular, "a falha não muta o agregado");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualizar preserva curso, local e unidade ofertante (imutáveis — não trafegam no comando)")]
    public async Task Handle_Atualizacao_PreservaImutaveis()
    {
        UnidadeOfertante unidade = Unidade();
        OfertaCurso oferta = OfertaExistente(unidade);
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(oferta.Id, programa: "OUTRO", baseLegal: "Resolução CONSEPE 1/2026"),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        oferta.CursoId.Should().Be(CursoId);
        oferta.LocalOfertaId.Should().Be(LocalOfertaId);
        oferta.UnidadeOfertante.Should().Be(unidade, "o snapshot congelado (ADR-0061) é imutável pós-criação");
    }
}
