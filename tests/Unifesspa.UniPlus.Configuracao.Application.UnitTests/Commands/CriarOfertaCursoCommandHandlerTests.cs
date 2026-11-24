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
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CriarOfertaCursoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IOfertaCursoRepository _repository = Substitute.For<IOfertaCursoRepository>();
    private readonly ICursoRepository _cursoRepository = Substitute.For<ICursoRepository>();
    private readonly ILocalOfertaRepository _localOfertaRepository = Substitute.For<ILocalOfertaRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static Curso CursoVivo() =>
        Curso.Criar("ENG_CIVIL", "Engenharia Civil", "Bacharelado", "Graduação", null).ValorEsperado();

    private static LocalOferta LocalVivo() =>
        LocalOferta.Criar(
            new("CAMPUS_SEDE", null, "1504208", "Marabá", "PA", null, null),
            Agora).ValorEsperado();

    private static UnidadeView UnidadeViva(Guid id) =>
        new(id, "FACET", "FACET", "facet", "Faculdade de Computação e Engenharia Elétrica",
            null, "Faculdade", UnidadeAcademica: true, UnidadeSuperiorId: null);

    private static bool CongelaAUnidadeViva(OfertaCurso oferta, Guid unidadeId, Guid cursoId, Guid localId) =>
        oferta.UnidadeOfertante.OrigemId == unidadeId
        && oferta.UnidadeOfertante.Sigla == "FACET"
        && oferta.UnidadeOfertante.Nome == "Faculdade de Computação e Engenharia Elétrica"
        && oferta.UnidadeOfertante.Tipo == "Faculdade"
        && oferta.CursoId == cursoId
        && oferta.LocalOfertaId == localId;

    private static CriarOfertaCursoCommand Comando(
        Guid cursoId,
        Guid localOfertaId,
        Guid unidadeOrigemId,
        string programa = "REGULAR",
        string? baseLegal = null) =>
        new(cursoId, localOfertaId, unidadeOrigemId, "ENF_BACHARELADO_MARABA", programa,
            RegimeDeFuncionamento: "EXTENSIVO", RegimeDeTurno: "REGULAR", Turnos: ["MATUTINO"],
            FormatoPedagogico: "PRESENCIAL", EMecCodigo: "123456", CodigoSga: "ENG-01",
            VagasAnuaisAutorizadas: 40, BaseLegal: baseLegal, AtoAutorizacaoMec: null);

    private Task<Result<Guid>> HandleAsync(CriarOfertaCursoCommand comando) =>
        CriarOfertaCursoCommandHandler.Handle(
            comando, new ConferenciaDasReferenciasDaOferta(_cursoRepository, _localOfertaRepository, _unidadeReader),
            _repository, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Curso e local vivos + unidade viva: congela o snapshot da view, persiste e retorna o Id")]
    public async Task Handle_ReferenciasVivas_CongelaEPersiste()
    {
        PrepararReferenciasVivas(out Guid cursoId, out Guid localId, out Guid unidadeId);

        Result<Guid> resultado = await HandleAsync(Comando(cursoId, localId, unidadeId));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        // Cópia dos dados (ADR-0061): sigla/nome/tipo congelados exatamente como a view devolveu.
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<OfertaCurso>(o => CongelaAUnidadeViva(o, unidadeId, cursoId, localId)),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Curso inexistente ou removido retorna CursoInexistenteOuRemovido sem consultar o reader")]
    public async Task Handle_CursoInexistente_RetornaErro()
    {
        Guid cursoId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(cursoId, Arg.Any<CancellationToken>()).Returns((Curso?)null);

        Result<Guid> resultado = await HandleAsync(
            Comando(cursoId, Guid.CreateVersion7(), Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CursoInexistenteOuRemovido);
        await _unidadeReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<OfertaCurso>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Local de oferta inexistente ou removido retorna LocalOfertaInexistenteOuRemovido sem persistir")]
    public async Task Handle_LocalInexistente_RetornaErro()
    {
        Curso curso = CursoVivo();
        Guid localId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(localId, Arg.Any<CancellationToken>()).Returns((LocalOferta?)null);

        Result<Guid> resultado = await HandleAsync(Comando(curso.Id, localId, Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.LocalOfertaInexistenteOuRemovido);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<OfertaCurso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reader devolve null: UnidadeOfertanteInexistente — não há identidade viva para congelar (ADR-0061)")]
    public async Task Handle_UnidadeInexistente_RetornaErro()
    {
        Curso curso = CursoVivo();
        LocalOferta local = LocalVivo();
        Guid unidadeId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _unidadeReader.ObterPorIdAsync(unidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result<Guid> resultado = await HandleAsync(Comando(curso.Id, local.Id, unidadeId));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteInexistente);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<OfertaCurso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Programa não-REGULAR sem base legal propaga o erro de domínio sem persistir")]
    public async Task Handle_ProgramaNaoRegularSemBaseLegal_RetornaErroSemPersistir()
    {
        Curso curso = CursoVivo();
        LocalOferta local = LocalVivo();
        Guid unidadeId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _unidadeReader.ObterPorIdAsync(unidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeViva(unidadeId));

        Result<Guid> resultado = await HandleAsync(
            Comando(curso.Id, local.Id, unidadeId, programa: "PARFOR", baseLegal: null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem consultar curso, local ou unidade — validação vence I/O")]
    public async Task Handle_CampoInvalido_RetornaErroSemConsultarNadaNemPersistir()
    {
        var comando = new CriarOfertaCursoCommand(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "ENF_BACHARELADO_MARABA",
            ProgramaDeOferta: "PROUNI", RegimeDeFuncionamento: "EXTENSIVO",
            RegimeDeTurno: "REGULAR", Turnos: ["MATUTINO"]);

        Result<Guid> resultado = await HandleAsync(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
        await _cursoRepository.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unidadeReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já usado por oferta viva é recusado com CodigoJaEmUso, sem persistir")]
    public async Task Handle_CodigoJaEmUso_RetornaConflitoSemPersistir()
    {
        PrepararReferenciasVivas(out Guid cursoId, out Guid localId, out Guid unidadeId);
        _repository.CodigoExisteEntreVivosAsync("ENF_BACHARELADO_MARABA", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await HandleAsync(Comando(cursoId, localId, unidadeId));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoJaEmUso);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<OfertaCurso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código com formato inválido é recusado em erro de campo codigo, sem consultar curso, local ou unidade")]
    public async Task Handle_CodigoInvalido_RetornaErroDeCampoSemConsultarNada()
    {
        var comando = new CriarOfertaCursoCommand(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "enf-maraba",
            ProgramaDeOferta: "REGULAR", RegimeDeFuncionamento: "EXTENSIVO", RegimeDeTurno: "REGULAR", Turnos: ["MATUTINO"]);

        Result<Guid> resultado = await HandleAsync(comando);

        resultado.Errors.Should().ContainSingle(e => e.Field == "codigo" && e.Error.Code == OfertaCursoErrorCodes.CodigoFormatoInvalido);
        await _cursoRepository.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida no índice único do código: a violação é informada como CodigoJaEmUso e o rastreamento é descartado")]
    public async Task Handle_ViolacaoDoIndiceDoCodigo_RetornaConflitoEDescarta()
    {
        PrepararReferenciasVivas(out Guid cursoId, out Guid localId, out Guid unidadeId);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.OfertaDeCursoCodigoAtivos)));

        Result<Guid> resultado = await HandleAsync(Comando(cursoId, localId, unidadeId));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoJaEmUso);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private void PrepararReferenciasVivas(out Guid cursoId, out Guid localId, out Guid unidadeId)
    {
        Curso curso = CursoVivo();
        LocalOferta local = LocalVivo();
        unidadeId = Guid.CreateVersion7();
        cursoId = curso.Id;
        localId = local.Id;
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _unidadeReader.ObterPorIdAsync(unidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeViva(unidadeId));
    }

    [Fact(DisplayName = "Programa não-REGULAR com base legal cria normalmente")]
    public async Task Handle_ProgramaNaoRegularComBaseLegal_Cria()
    {
        PrepararReferenciasVivas(out Guid cursoId, out Guid localId, out Guid unidadeId);

        Result<Guid> resultado = await HandleAsync(
            Comando(cursoId, localId, unidadeId, programa: "PARFOR", baseLegal: "Decreto 6.755/2009"));

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<OfertaCurso>(o =>
                o.ProgramaDeOferta == ProgramaDeOferta.Parfor
                && o.BaseLegal == "Decreto 6.755/2009"),
            Arg.Any<CancellationToken>());
    }
}
