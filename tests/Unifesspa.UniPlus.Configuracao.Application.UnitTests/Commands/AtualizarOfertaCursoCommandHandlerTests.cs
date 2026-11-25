namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
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
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static UnidadeOfertante Unidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    private static OfertaCurso OfertaExistente(UnidadeOfertante? unidade = null) =>
        OfertaCurso.Criar(
            CursoId, LocalOfertaId, unidade ?? Unidade(),
            new DeclaracaoDaOfertaDeCurso(
                "ENF_BACHARELADO_MARABA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"], "123456", "ENG-01", 40)).Value!;

    private static AtualizarOfertaCursoCommand Comando(
        Guid id,
        string codigo = "ENF_BACH_MARABA",
        string programa = "REGULAR",
        string? baseLegal = null) =>
        new(id, codigo, programa, RegimeDeFuncionamento: "EXTENSIVO", RegimeDeTurno: "INTEGRAL",
            Turnos: ["NOTURNO", "VESPERTINO"],
            FormatoPedagogico: "EAD", CodigoEmec: "654321", CodigoNoSistemaAcademico: "ENG-02",
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
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        oferta.Turnos.Should().Equal(TurnoOferta.Vespertino, TurnoOferta.Noturno);
        oferta.CodigoEmec.Should().Be("654321");
        oferta.CodigoNoSistemaAcademico.Should().Be("ENG-02");
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

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem buscar a oferta por Id — validação vence 404")]
    public async Task Handle_CampoInvalido_RetornaErroSemBuscarPorId()
    {
        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(Guid.CreateVersion7(), programa: "PROUNI"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização troca o código da oferta e mantém a identidade")]
    public async Task Handle_TrocaDeCodigo_PersisteMantendoOId()
    {
        OfertaCurso oferta = OfertaExistente();
        Guid id = oferta.Id;
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(oferta);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(id, codigo: "ENF_BACH_MARABA"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        oferta.Codigo.Should().Be("ENF_BACH_MARABA");
        oferta.Id.Should().Be(id);
        await _repository.Received(1).CodigoExisteEntreVivosAsync("ENF_BACH_MARABA", id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código de outra oferta viva é recusado com CodigoJaEmUso, sem persistir")]
    public async Task Handle_CodigoDeOutraOferta_RetornaConflito()
    {
        OfertaCurso oferta = OfertaExistente();
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);
        _repository.CodigoExisteEntreVivosAsync("ENF_BACH_MARABA", oferta.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(oferta.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoJaEmUso);
        resultado.Error.Message.Should().NotContain("ENF_BACH_MARABA");
        oferta.Codigo.Should().Be("ENF_BACHARELADO_MARABA", "a recusa vem antes de qualquer mutação");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida no índice único do código: a violação vira CodigoJaEmUso e o rastreamento é descartado")]
    public async Task Handle_ViolacaoDoIndiceDoCodigo_RetornaConflitoEDescarta()
    {
        OfertaCurso oferta = OfertaExistente();
        _repository.ObterPorIdAsync(oferta.Id, Arg.Any<CancellationToken>()).Returns(oferta);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.OfertaDeCursoCodigoAtivos)));

        Result resultado = await AtualizarOfertaCursoCommandHandler.Handle(
            Comando(oferta.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoJaEmUso);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
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
