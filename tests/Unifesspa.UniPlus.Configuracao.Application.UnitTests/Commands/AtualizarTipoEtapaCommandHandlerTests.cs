namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AtualizarTipoEtapaCommandHandlerTests
{
    private readonly ITipoEtapaRepository _repository = Substitute.For<ITipoEtapaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoEtapa Existente(string codigo = "CODIGO_EXISTENTE") =>
        TipoEtapa.Criar(codigo, "Nome original", null, true, true).Value!;

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoEtapa?)null);

        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(id, "Nome novo", true, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        TipoEtapa existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(existente.Id, "Nome atualizado", true, true, "Descrição nova"),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Should().Be("CODIGO_EXISTENTE", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Nome atualizado");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A validação de campo vence o "não encontrado", e um payload mal formado não chega a
    /// ObterPorIdAsync (ADR-0125, item 5).
    /// </summary>
    [Fact(DisplayName = "Id inexistente com Nome vazio devolve a violação de campo, não NaoEncontrado, sem consultar o repositório")]
    public async Task Handle_IdInexistenteComNomeVazio_RetornaViolacaoDeCampoSemConsultarRepositorio()
    {
        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "", true, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome vazio e descrição longa no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeVazioEDescricaoLonga_AcumulaAsDuasViolacoes()
    {
        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "", true, true, new string('a', 1001)),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "Desligar a pontuação do tipo de nota do ENEM devolve a recusa do tipo e não persiste")]
    public async Task Handle_NotaDeOrigemNoEnemSemPontuacao_RecusaSemPersistir()
    {
        TipoEtapa existente = TipoEtapaDeTeste.NotaDoEnem();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(existente.Id, "Nome novo", false, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(TipoEtapaErrorCodes.NotaDeOrigemNoEnemExigePontuacao);
        existente.AdmitePontuacao.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A recusa que depende do tipo só aparece com os campos válidos: com campo inválido o
    /// handler não busca o tipo (ADR-0125, item 5).
    /// </summary>
    [Fact(DisplayName = "Campo inválido e pontuação desligada no tipo do ENEM devolvem só a violação de campo, sem buscar o tipo")]
    public async Task Handle_CampoInvalidoESemPontuacaoNoTipoDoEnem_DevolveSoOCampo()
    {
        TipoEtapa existente = TipoEtapaDeTeste.NotaDoEnem();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoEtapaCommandHandler.Handle(
            new AtualizarTipoEtapaCommand(existente.Id, "", false, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(("nome", TipoEtapaErrorCodes.NomeObrigatorio));
        existente.Nome.Should().Be("Nota do ENEM");
        existente.AdmitePontuacao.Should().BeTrue();
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
