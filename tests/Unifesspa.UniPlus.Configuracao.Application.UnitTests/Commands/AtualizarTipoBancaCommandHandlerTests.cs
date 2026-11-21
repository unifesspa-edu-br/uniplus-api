namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class AtualizarTipoBancaCommandHandlerTests
{
    private readonly ITipoBancaRepository _repository = Substitute.For<ITipoBancaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static TipoBanca Existente(string codigo = "BANCA_ANALISE_RECURSOS") =>
        TipoBanca.Criar(codigo, new DeclaracaoDoTipoDeBanca("Banca de análise de recursos", Papel: "JULGA_A_PRIMEIRA_INSTANCIA")).Value!;

    [Fact(DisplayName = "Tipo de banca inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoBanca?)null);

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            new AtualizarTipoBancaCommand(id, Nome: "x", Papel: "IMPORTA_CANDIDATOS"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Antes do validator removido, um payload mal formado nunca chegava a
    /// ObterPorIdAsync — validação sempre vencia sobre "não encontrado". Sem o
    /// validator, o handler precisa preservar essa prioridade explicitamente.
    /// </summary>
    [Fact(DisplayName = "Id inexistente com Nome vazio devolve a violação de campo, não NaoEncontrado")]
    public async Task Handle_IdInexistenteComNomeVazio_RetornaViolacaoDeCampoSemConsultarRepositorio()
    {
        Guid idInexistente = Guid.CreateVersion7();

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            new AtualizarTipoBancaCommand(idInexistente, Nome: ""), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome vazio e descrição longa no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeVazioEDescricaoLonga_AcumulaAsDuasViolacoes()
    {
        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            new AtualizarTipoBancaCommand(Guid.CreateVersion7(), Nome: "", Descricao: new string('a', 301), Papel: "IMPORTA_CANDIDATOS"),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        TipoBanca existente = Existente("BANCA_ANALISE_RECURSOS");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarTipoBancaCommand(existente.Id, Nome: "Banca de recursos (novo)", FaseTipica: "Recursos", Papel: "JULGA_A_PRIMEIRA_INSTANCIA");

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Should().Be(TipoDeBanca.AnaliseDeRecursos, "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Banca de recursos (novo)");
        existente.FaseTipica.Should().Be("Recursos");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Id inexistente com papel ausente devolve a violação do papel, não NaoEncontrado")]
    public async Task Handle_IdInexistenteSemPapel_RetornaPapelObrigatorioSemConsultarRepositorio()
    {
        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            new AtualizarTipoBancaCommand(Guid.CreateVersion7(), Nome: "x"), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.PapelObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização troca o papel, as marcas e os tipos de decisão, e persiste")]
    public async Task Handle_TrocaAAtuacao_Persiste()
    {
        TipoBanca existente = Existente("BANCA_ANALISE_RECURSOS");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        var comando = new AtualizarTipoBancaCommand(
            existente.Id, Nome: "Banca de recursos", Papel: "AVALIA_O_MERITO", ExigeDuasOuMaisBancas: true,
            TiposDeDecisaoJulgados: ["CLASSIFICACAO", "NOTA_DA_ETAPA"]);

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Atuacao.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        existente.Atuacao.ExigeDuasOuMaisBancas.Should().BeTrue();
        existente.Atuacao.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.NotaDaEtapa);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
