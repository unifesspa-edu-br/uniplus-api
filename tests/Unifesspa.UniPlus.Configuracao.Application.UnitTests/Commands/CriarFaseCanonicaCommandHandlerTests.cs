namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarFaseCanonicaCommandHandlerTests
{
    private readonly IFaseCanonicaRepository _repository = Substitute.For<IFaseCanonicaRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarFaseCanonicaCommand Comando() =>
        new("INSCRICAO", Nome: "Inscrição", OrigemData: "NO_EDITAL");

    [Fact(DisplayName = "Código livre cria a fase, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("INSCRICAO", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando(), _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<FaseCanonica>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("INSCRICAO", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando(), _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<FaseCanonica>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do conjunto canônico propaga o erro sem consultar unicidade nem persistir")]
    public async Task Handle_ForaDoCanonico_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        var comando = new CriarFaseCanonicaCommand("ENTREVISTA_FINAL", Nome: "x", OrigemData: "NO_EDITAL");

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Invariante de coerência (agrupar etapas fora da avaliação) propaga o erro sem persistir")]
    public async Task Handle_CoerenciaInvalida_RetornaErroSemPersistir()
    {
        _repository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);

        var comando = new CriarFaseCanonicaCommand("HOMOLOGACAO_INSCRICOES", Nome: "Homologação", AgrupaEtapas: true, OrigemData: "NO_EDITAL");

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do conjunto canônico com inscrição e isenção ligadas acumula o código e a exclusividade, sem persistir")]
    public async Task Handle_ForaDoCanonicoComAsDuasMarcas_AcumulaCodigoEExclusividade()
    {
        var comando = new CriarFaseCanonicaCommand(
            "ENTREVISTA_FINAL", Nome: "Entrevista", ColetaInscricao: true, ColetaSolicitacaoIsencao: true, OrigemData: "NO_EDITAL");

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("codigo", FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        var comando = new CriarFaseCanonicaCommand(null, Nome: "", OrigemData: "NO_EDITAL");

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }

    [Fact(DisplayName = "Unidade sugerida viva é copiada para a fase com sigla e nome do cadastro")]
    public async Task Handle_UnidadeSugeridaViva_CopiaSiglaENome()
    {
        UnidadeView cga = UnidadeCga();
        _unidadeReader.ObterPorIdAsync(cga.Id, Arg.Any<CancellationToken>()).Returns(cga);
        FaseCanonica? gravada = null;
        await _repository.AdicionarAsync(Arg.Do<FaseCanonica>(f => gravada = f), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando() with { UnidadeSugeridaId = cga.Id }, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        gravada!.UnidadeSugerida.Should().Be(new UnidadeSugerida(cga.Id, "CGA", "Centro de Gestão Acadêmica"));
    }

    [Fact(DisplayName = "Fase sem unidade sugerida é aceita e não consulta o cadastro de unidades")]
    public async Task Handle_SemUnidadeSugerida_AceitaSemConsultarOCadastro()
    {
        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando(), _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _unidadeReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade sugerida inexistente ou inativa é recusada no campo unidadeSugeridaId sem persistir")]
    public async Task Handle_UnidadeSugeridaNaoViva_RecusaNoCampoSemPersistir()
    {
        Guid ausente = Guid.CreateVersion7();
        _unidadeReader.ObterPorIdAsync(ausente, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando() with { UnidadeSugeridaId = ausente }, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("unidadeSugeridaId");
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.UnidadeSugeridaNaoEncontrada);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<FaseCanonica>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido e unidade inexistente acumulam as duas recusas, o campo antes da unidade, sem persistir")]
    public async Task Handle_CampoInvalidoEUnidadeInexistente_AcumulaAsDuasRecusas()
    {
        Guid ausente = Guid.CreateVersion7();
        _unidadeReader.ObterPorIdAsync(ausente, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        var comando = new CriarFaseCanonicaCommand("INSCRICAO", Nome: "", OrigemData: "NO_EDITAL", UnidadeSugeridaId: ausente);

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(e => e.Field).Should().Equal("nome", "unidadeSugeridaId");
        resultado.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
        resultado.Errors[1].Error.Code.Should().Be(FaseCanonicaErrorCodes.UnidadeSugeridaNaoEncontrada);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<FaseCanonica>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Perder a corrida do índice único do código descarta a inserção e retorna CodigoJaExiste")]
    public async Task Handle_CorridaDoIndiceUnico_DescartaERetornaConflito()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.FaseCanonicaCodigoVivo)));

        Result<Guid> resultado = await CriarFaseCanonicaCommandHandler.Handle(
            Comando(), _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static UnidadeView UnidadeCga() => new(
        Guid.CreateVersion7(), "CGA", "CGA", "cga", "Centro de Gestão Acadêmica", null, "Centro", true, null);
}
