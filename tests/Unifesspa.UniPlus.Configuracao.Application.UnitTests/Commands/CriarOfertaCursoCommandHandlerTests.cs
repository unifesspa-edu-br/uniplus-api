namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarOfertaCursoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IOfertaCursoRepository _repository = Substitute.For<IOfertaCursoRepository>();
    private readonly ICursoRepository _cursoRepository = Substitute.For<ICursoRepository>();
    private readonly ILocalOfertaRepository _localOfertaRepository = Substitute.For<ILocalOfertaRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Curso CursoVivo() =>
        Curso.Criar("ENG_CIVIL", "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;

    private static LocalOferta LocalVivo() =>
        LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

    private static UnidadeView UnidadeViva(Guid id) =>
        new(id, "FACET", "facet", "Faculdade de Computação e Engenharia Elétrica",
            null, "Faculdade", UnidadeAcademica: true, UnidadeSuperiorId: null);

    private static CriarOfertaCursoCommand Comando(
        Guid cursoId,
        Guid localOfertaId,
        Guid unidadeOrigemId,
        string programa = "REGULAR",
        string? baseLegal = null) =>
        new(cursoId, localOfertaId, unidadeOrigemId, programa,
            FormatoPedagogico: "PRESENCIAL", Turno: "MATUTINO",
            EMecCodigo: "123456", CodigoSga: "ENG-01",
            VagasAnuaisAutorizadas: 40, BaseLegal: baseLegal, AtoAutorizacaoMec: null);

    private Task<Result<Guid>> HandleAsync(CriarOfertaCursoCommand comando) =>
        CriarOfertaCursoCommandHandler.Handle(
            comando, _repository, _cursoRepository, _localOfertaRepository,
            _unidadeReader, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Curso e local vivos + unidade viva: congela o snapshot da view, persiste e retorna o Id")]
    public async Task Handle_ReferenciasVivas_CongelaEPersiste()
    {
        Curso curso = CursoVivo();
        LocalOferta local = LocalVivo();
        Guid unidadeId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _unidadeReader.ObterPorIdAsync(unidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeViva(unidadeId));

        Result<Guid> resultado = await HandleAsync(Comando(curso.Id, local.Id, unidadeId));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        // Snapshot-copy (ADR-0061): sigla/nome/tipo congelados exatamente como a view devolveu.
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<OfertaCurso>(o =>
                o.UnidadeOfertante.OrigemId == unidadeId
                && o.UnidadeOfertante.Sigla == "FACET"
                && o.UnidadeOfertante.Nome == "Faculdade de Computação e Engenharia Elétrica"
                && o.UnidadeOfertante.Tipo == "Faculdade"
                && o.CursoId == curso.Id
                && o.LocalOfertaId == local.Id),
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

    [Fact(DisplayName = "Programa não-REGULAR com base legal cria normalmente")]
    public async Task Handle_ProgramaNaoRegularComBaseLegal_Cria()
    {
        Curso curso = CursoVivo();
        LocalOferta local = LocalVivo();
        Guid unidadeId = Guid.CreateVersion7();
        _cursoRepository.ObterPorIdParaLeituraAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _localOfertaRepository.ObterPorIdParaLeituraAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _unidadeReader.ObterPorIdAsync(unidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeViva(unidadeId));

        Result<Guid> resultado = await HandleAsync(
            Comando(curso.Id, local.Id, unidadeId, programa: "PARFOR", baseLegal: "Decreto 6.755/2009"));

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<OfertaCurso>(o =>
                o.ProgramaDeOferta == ProgramaDeOferta.Parfor
                && o.BaseLegal == "Decreto 6.755/2009"),
            Arg.Any<CancellationToken>());
    }
}
