namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AtualizarTipoEtapaCommandHandlerTests
{
    private readonly ITipoEtapaRepository _repository = Substitute.For<ITipoEtapaRepository>();
    private readonly ITipoConceitoRepository _conceitos = Substitute.For<ITipoConceitoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private Task<Result> Atualizar(AtualizarTipoEtapaCommand comando) =>
        AtualizarTipoEtapaCommandHandler.Handle(comando, _repository, _conceitos, _unitOfWork, CancellationToken.None);

    private void CatalogoTem(params TipoConceito[] tipos) =>
        _conceitos.ListarPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(tipos);

    private static TipoEtapa Existente(string codigo = "CODIGO_EXISTENTE") =>
        TipoEtapa.Criar(codigo, "Nome original", null, true, true, false).Value!;

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoEtapa?)null);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(id, "Nome novo", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        TipoEtapa existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome atualizado", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir(), "Descrição nova"));

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
        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome vazio e descrição longa no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeVazioEDescricaoLonga_AcumulaAsDuasViolacoes()
    {
        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir(), new string('a', 1001)));

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

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome novo", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(false).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir()));

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

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(false).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(("nome", TipoEtapaErrorCodes.NomeObrigatorio));
        existente.Nome.Should().Be("Nota do ENEM");
        existente.AdmitePontuacao.Should().BeTrue();
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização substitui os conceitos admitidos pelo conjunto informado")]
    public async Task Handle_ConceitosInformados_SubstituiOConjunto()
    {
        TipoEtapa existente = Existente();
        existente.SubstituirConceitosAdmitidos([TiposConceitoDeTeste.Categorico()]);
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        CatalogoTem(nota);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos(nota.Codigo).Construir()));

        resultado.IsSuccess.Should().BeTrue();
        existente.ConceitosAdmitidos.Should().ContainSingle().Which.Should().BeSameAs(nota);
    }

    [Fact(DisplayName = "Lista ausente é recusada como campo obrigatório, sem buscar o tipo nem consultar o catálogo")]
    public async Task Handle_ListaAusente_RecusaSemBuscarOTipo()
    {
        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos(null).Construir()));

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("avaliacao.conceitosAdmitidos", TipoEtapaErrorCodes.ConceitosAdmitidosObrigatorio));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _conceitos.DidNotReceive().ListarPorCodigosAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conceito inexistente acumula com a violação de campo, não busca o tipo e preserva o conjunto")]
    public async Task Handle_ConceitoInexistenteENomeAusente_AcumulaSemBuscarOTipo()
    {
        CatalogoTem();

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(Guid.CreateVersion7(), "", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos("FANTASMA").Construir()));

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("nome", TipoEtapaErrorCodes.NomeObrigatorio),
            ("avaliacao.conceitosAdmitidos", TipoEtapaErrorCodes.ConceitoInexistente));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa do tipo de nota do ENEM não altera os conceitos admitidos")]
    public async Task Handle_RecusaDoTipo_NaoSubstituiOsConceitos()
    {
        TipoEtapa existente = TipoEtapaDeTeste.NotaDoEnem();
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        CatalogoTem(nota);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(false).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos(nota.Codigo).Construir()));

        resultado.IsFailure.Should().BeTrue();
        existente.ConceitosAdmitidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) ao substituir os conceitos descarta o rastreamento e devolve o conflito")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaRastreamentoEDevolveConflito()
    {
        TipoEtapa existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome atualizado", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(false).ComConceitos().Construir()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Exigir correção independente com lista de conceitos vazia é recusado, sem alterar o tipo nem persistir")]
    public async Task Handle_CorrecaoIndependenteSemConceito_RecusaSemPersistir()
    {
        TipoEtapa existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(true).ComConceitos().Construir()));

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("avaliacao.conceitosAdmitidos", TipoEtapaErrorCodes.CorrecaoIndependenteSemConceito));
        existente.ExigeCorrecaoIndependente.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ligar a correção independente com um conceito admitido persiste o sinalizador")]
    public async Task Handle_CorrecaoIndependenteComConceito_Persiste()
    {
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        CatalogoTem(nota);
        TipoEtapa existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(true).ComConceitos(nota.Codigo).Construir()));

        resultado.IsSuccess.Should().BeTrue();
        existente.ExigeCorrecaoIndependente.Should().BeTrue();
    }

    [Fact(DisplayName = "Tipo com nota de origem no ENEM recusa exigir correção independente, mesmo com conceito admitido")]
    public async Task Handle_NotaDeOrigemNoEnemComCorrecaoIndependente_Recusa()
    {
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        CatalogoTem(nota);
        TipoEtapa existente = TipoEtapaDeTeste.NotaDoEnem();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await Atualizar(new AtualizarTipoEtapaCommand(existente.Id, "Nome", AvaliacaoDasEtapasDeTeste.Padrao().ComPontuacao(true).ComEliminacao(true).ComCorrecaoIndependente(true).ComConceitos(nota.Codigo).Construir()));

        resultado.Errors.Select(erro => erro.Error.Code).Should().Equal(TipoEtapaErrorCodes.CorrecaoIndependenteComNotaDeOrigemNoEnem);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
