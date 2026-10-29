namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
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
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static FaseCanonica Existente(string codigo = "ENSALAMENTO") =>
        FaseCanonica.Criar(codigo, "Ensalamento", null, false, false, null, false, false, "NO_EDITAL").Value!;

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
        FaseCanonica existente = Existente("HOMOLOGACAO");
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

    [Fact(DisplayName = "Unidade sugerida viva troca a sugestão da fase pela cópia atual do cadastro")]
    public async Task Handle_UnidadeSugeridaViva_TrocaASugestao()
    {
        FaseCanonica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        UnidadeView cga = new(Guid.CreateVersion7(), "CGA", "cga", "Centro de Gestão Acadêmica", null, "Centro", true, null);
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

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(
                existente.Id, Nome: "Ensalamento (novo)", OrigemData: "NO_EDITAL", UnidadeSugeridaId: gravada.OrigemId),
            _repository, _unidadeReader, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Ensalamento (novo)");
        existente.UnidadeSugerida.Should().Be(gravada);
        await _unidadeReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
