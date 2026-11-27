namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarFaseCanonicaCommandHandlerTests
{
    private readonly IFaseCanonicaRepository _repository = Substitute.For<IFaseCanonicaRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static FaseCanonica Existente(string codigo = "ENSALAMENTO") =>
        FaseCanonica.Criar(
            codigo, "Ensalamento", null, null, "NO_EDITAL",
            new SinalizadoresDaFase(AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrganizadaEmChamadas: false)).Value!;

    private Task<Result> Executar(AtualizarFaseCanonicaCommand comando) =>
        AtualizarFaseCanonicaCommandHandler.Handle(comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Os sinalizadores do comando de atualização levam a marca de organizada em chamadas à fase")]
    public void Sinalizadores_LevamAMarcaDeOrganizadaEmChamadas()
    {
        new AtualizarFaseCanonicaCommand(Guid.NewGuid(), OrganizadaEmChamadas: true).Sinalizadores()
            .OrganizadaEmChamadas.Should().BeTrue();
        new AtualizarFaseCanonicaCommand(Guid.NewGuid()).Sinalizadores()
            .OrganizadaEmChamadas.Should().BeFalse();
    }

    [Fact(DisplayName = "Fase inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(id, Nome: "x", OrigemData: "NO_EDITAL"), _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        FaseCanonica existente = Existente("ENSALAMENTO");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Ensalamento (novo)", OrigemData: "NO_EDITAL");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Ensalamento (novo)");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coerência dependente do código (agrupar etapas) busca a fase por Id e só então recusa (422) sem persistir")]
    public async Task Handle_CoerenciaInvalida_BuscaPorIdERecusa422SemPersistir()
    {
        FaseCanonica existente = Existente("HOMOLOGACAO_INSCRICOES");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Homologação", AgrupaEtapas: true, OrigemData: "NO_EDITAL");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
            "essa coerência só é decidível depois do fetch, porque depende do código persistido (imutável)");
        await _repository.Received(1).ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente (independente do código) retorna a violação sem buscar a fase por Id")]
    public async Task Handle_NomeAusente_RetornaViolacaoSemBuscarPorId()
    {
        var comando = new AtualizarFaseCanonicaCommand(
            Guid.CreateVersion7(), Nome: "", OrigemData: "NO_EDITAL");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente com inscrição e isenção ligadas acumula as duas recusas sem buscar a fase por Id")]
    public async Task Handle_NomeAusenteEAsDuasMarcas_AcumulaSemBuscarPorId()
    {
        var comando = new AtualizarFaseCanonicaCommand(
            Guid.CreateVersion7(), Nome: "", ColetaInscricao: true, ColetaSolicitacaoIsencao: true, OrigemData: "NO_EDITAL");

        Result resultado = await Executar(comando);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("nome", FaseCanonicaErrorCodes.NomeObrigatorio),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade sugerida viva troca a sugestão da fase pela cópia atual do cadastro")]
    public async Task Handle_UnidadeSugeridaViva_TrocaASugestao()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        UnidadeView cga = new(Guid.CreateVersion7(), "CGA", "CGA", "cga", "Centro de Gestão Acadêmica", null, "Centro", true, null);
        _unidadeReader.ObterPorIdAsync(cga.Id, Arg.Any<CancellationToken>()).Returns(cga);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Ensalamento", OrigemData: "NO_EDITAL", UnidadeSugeridaId: cga.Id),
            _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.UnidadeSugerida.Should().Be(new UnidadeSugerida(cga.Id, "CGA", "Centro de Gestão Acadêmica"));
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade sugerida nula no PUT retira a sugestão da fase")]
    public async Task Handle_UnidadeSugeridaNula_RetiraASugestao()
    {
        FaseCanonica existente = Existente();
        existente.DefinirUnidadeSugerida(new UnidadeSugerida(Guid.CreateVersion7(), "CGA", "Centro de Gestão Acadêmica"));
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Ensalamento", OrigemData: "NO_EDITAL"),
            _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.UnidadeSugerida.Should().BeNull();
        existente.UnidadeSugeridaOrigemId.Should().BeNull();
    }

    [Fact(DisplayName = "Unidade sugerida inexistente ou inativa é recusada no campo unidadeSugeridaId sem persistir")]
    public async Task Handle_UnidadeSugeridaNaoViva_RecusaNoCampoSemPersistir()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        Guid ausente = Guid.CreateVersion7();
        _unidadeReader.ObterPorIdAsync(ausente, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Ensalamento", OrigemData: "NO_EDITAL", UnidadeSugeridaId: ausente),
            _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("unidadeSugeridaId");
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.UnidadeSugeridaNaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reenviar a unidade sugerida já gravada, depois apagada no cadastro, aceita editar o nome e mantém a sugestão")]
    public async Task Handle_UnidadeGravadaDepoisApagada_EditaOutroCampoEMantemASugestao()
    {
        FaseCanonica existente = Existente();
        UnidadeSugerida gravada = new(Guid.CreateVersion7(), "CGA", "Centro de Gestão Acadêmica");
        existente.DefinirUnidadeSugerida(gravada);
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unidadeReader.ObterPorIdAsync(gravada.OrigemId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result resultado = await Executar(new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Ensalamento (novo)", OrigemData: "NO_EDITAL", UnidadeSugeridaId: gravada.OrigemId));

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Ensalamento (novo)");
        existente.UnidadeSugerida.Should().Be(gravada);
        await _unidadeReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade sugerida inexistente descarta a edição já aplicada à fase rastreada e não grava")]
    public async Task Handle_UnidadeSugeridaNaoViva_DescartaAEdicaoENaoGrava()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        Guid ausente = Guid.CreateVersion7();
        _unidadeReader.ObterPorIdAsync(ausente, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result resultado = await Executar(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Ensalamento (novo)", OrigemData: "NO_EDITAL", UnidadeSugeridaId: ausente));

        resultado.IsFailure.Should().BeTrue();
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa do agregado descarta a edição rastreada e não grava")]
    public async Task Handle_RecusaDoAgregado_DescartaENaoGrava()
    {
        FaseCanonica existente = Existente("HOMOLOGACAO_INSCRICOES");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await Executar(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Homologação", AgrupaEtapas: true, OrigemData: "NO_EDITAL"));

        resultado.IsFailure.Should().BeTrue();
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa do agregado e unidade inexistente acumulam, a recusa do campo antes da unidade")]
    public async Task Handle_RecusaDoAgregadoEUnidadeInexistente_AcumulaAsDuasRecusas()
    {
        FaseCanonica existente = Existente("HOMOLOGACAO_INSCRICOES");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        Guid ausente = Guid.CreateVersion7();
        _unidadeReader.ObterPorIdAsync(ausente, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result resultado = await Executar(
            new AtualizarFaseCanonicaCommand(
                existente.Id, Nome: "Homologação", AgrupaEtapas: true, OrigemData: "NO_EDITAL", UnidadeSugeridaId: ausente));

        resultado.Errors.Select(e => e.Field).Should().Equal("agrupaEtapas", "unidadeSugeridaId");
    }

    [Fact(DisplayName = "Edição válida grava sem descartar o rastreamento")]
    public async Task Handle_EdicaoValida_NaoDescarta()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        await Executar(
            new AtualizarFaseCanonicaCommand(existente.Id, Nome: "Ensalamento", OrigemData: "NO_EDITAL"));

        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }
}
