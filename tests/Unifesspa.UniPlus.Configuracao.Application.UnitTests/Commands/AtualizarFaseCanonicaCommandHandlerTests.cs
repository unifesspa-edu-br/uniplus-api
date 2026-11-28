namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.ApoioDosComandosDeFaseCanonica;

public sealed class AtualizarFaseCanonicaCommandHandlerTests
{
    private readonly IFaseCanonicaRepository _repository = Substitute.For<IFaseCanonicaRepository>();
    private readonly IUnidadeReader _unidadeReader = Substitute.For<IUnidadeReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static FaseCanonica Existente(string codigo = "ENSALAMENTO") =>
        FaseCanonica.Criar(
            codigo, "Ensalamento", null, null, "NO_EDITAL",
            new SinalizadoresInformados(false, false, false, false, false)).Value!;

    private Task<Result> Executar(AtualizarFaseCanonicaCommand comando) =>
        AtualizarFaseCanonicaCommandHandler.Handle(comando, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Os sinalizadores do comando de atualização levam cada marca informada à fase, sem assumir valor")]
    public void Sinalizadores_LevamCadaMarcaInformada()
    {
        AtualizarFaseCanonicaCommand comando = Atualizacao(Guid.NewGuid()) with { OrganizadaEmChamadas = true, ColetaInscricao = null };

        comando.Sinalizadores().Should().Be(new SinalizadoresInformados(false, false, null, false, true));
    }

    [Fact(DisplayName = "Sinalizador ausente com fase inexistente recusa o sinalizador (422) sem consultar o repositório")]
    public async Task Handle_SinalizadorAusenteEFaseInexistente_RecusaSemConsultarORepositorio()
    {
        Result resultado = await Executar(Atualizacao(Guid.CreateVersion7()) with { AgrupaEtapas = null });

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("agrupaEtapas", CodigosDeValidacao.Obrigatorio));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fase inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            Atualizacao(id) with { Nome = "x" }, _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        FaseCanonica existente = Existente("ENSALAMENTO");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        AtualizarFaseCanonicaCommand comando = Atualizacao(existente.Id) with { Nome = "Ensalamento (novo)" };

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

        AtualizarFaseCanonicaCommand comando = Atualizacao(existente.Id) with { Nome = "Homologação", AgrupaEtapas = true };

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
        AtualizarFaseCanonicaCommand comando = Atualizacao(Guid.CreateVersion7()) with { Nome = "" };

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
        AtualizarFaseCanonicaCommand comando = Atualizacao(Guid.CreateVersion7()) with { Nome = "", ColetaInscricao = true, ColetaSolicitacaoIsencao = true };

        Result resultado = await Executar(comando);

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
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
            Atualizacao(existente.Id) with { Nome = "Ensalamento", UnidadeSugeridaId = cga.Id },
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
            Atualizacao(existente.Id) with { Nome = "Ensalamento" },
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
            Atualizacao(existente.Id) with { Nome = "Ensalamento", UnidadeSugeridaId = ausente },
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

        Result resultado = await Executar(Atualizacao(existente.Id) with { Nome = "Ensalamento (novo)", UnidadeSugeridaId = gravada.OrigemId });

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
            Atualizacao(existente.Id) with { Nome = "Ensalamento (novo)", UnidadeSugeridaId = ausente });

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
            Atualizacao(existente.Id) with { Nome = "Homologação", AgrupaEtapas = true });

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
            Atualizacao(existente.Id) with { Nome = "Homologação", AgrupaEtapas = true, UnidadeSugeridaId = ausente });

        resultado.Errors.Select(e => e.Field).Should().Equal("agrupaEtapas", "unidadeSugeridaId");
    }

    [Fact(DisplayName = "Edição válida grava sem descartar o rastreamento")]
    public async Task Handle_EdicaoValida_NaoDescarta()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        await Executar(
            Atualizacao(existente.Id) with { Nome = "Ensalamento" });

        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }
}
