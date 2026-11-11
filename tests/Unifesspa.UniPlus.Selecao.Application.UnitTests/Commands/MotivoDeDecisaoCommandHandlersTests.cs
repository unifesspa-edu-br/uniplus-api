namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Handlers do cadastro único de motivos de decisão: unicidade do código dentro
/// do tipo, conversão dos códigos do wire e ciclo de vida.
/// </summary>
public sealed class MotivoDeDecisaoCommandHandlersTests
{
    private const string Codigo = "RENDA_ACIMA_DO_LIMITE";
    private const string Descricao = "Renda familiar per capita acima do limite legal.";

    private readonly IMotivoDeDecisaoRepository _repository =
        Substitute.For<IMotivoDeDecisaoRepository>();

    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    [Fact(DisplayName = "Criar converte os códigos do wire e persiste o motivo")]
    public async Task Criar_PayloadValido_Persiste()
    {
        _repository.CodigoExisteAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Codigo, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarMotivoDeDecisaoCommandHandler.Handle(
            Comando(SentidoDoMotivoCodigo.Desfavoravel),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<MotivoDeDecisao>(motivo =>
                motivo.Tipo == TipoMotivoDeDecisao.IsencaoDaTaxa
                && motivo.Subclassificacao == FundamentoIsencaoCodigo.CadastroUnico
                && motivo.Sentido == SentidoDoMotivo.Desfavoravel),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Criar recusa código já usado sem tocar no repositório")]
    public async Task Criar_CodigoJaExiste_Recusa()
    {
        _repository.CodigoExisteAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Codigo, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarMotivoDeDecisaoCommandHandler.Handle(
            Comando(SentidoDoMotivoCodigo.Desfavoravel),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(
            Arg.Any<MotivoDeDecisao>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sentido fora do vocabulário do wire é recusado como ausente")]
    public async Task Criar_SentidoForaDoVocabulario_Recusa()
    {
        Result<Guid> resultado = await CriarMotivoDeDecisaoCommandHandler.Handle(
            Comando("TALVEZ"),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.SentidoObrigatorio);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desativar aplica a desativação e salva")]
    public async Task Desativar_MotivoAtivo_Salva()
    {
        MotivoDeDecisao motivo = MotivoValido();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await DesativarMotivoDeDecisaoCommandHandler.Handle(
            new DesativarMotivoDeDecisaoCommand(motivo.Id),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeFalse();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desativar motivo inexistente devolve não encontrado")]
    public async Task Desativar_MotivoInexistente_NaoEncontrado()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MotivoDeDecisao?)null);

        Result resultado = await DesativarMotivoDeDecisaoCommandHandler.Handle(
            new DesativarMotivoDeDecisaoCommand(Guid.CreateVersion7()),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Recusa do agregado descarta o rastreamento antes de devolver a falha")]
    public async Task Desativar_MotivoJaInativo_DescartaRastreamento()
    {
        MotivoDeDecisao motivo = MotivoValido();
        motivo.Desativar();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await DesativarMotivoDeDecisaoCommandHandler.Handle(
            new DesativarMotivoDeDecisaoCommand(motivo.Id),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaInativo);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reativar devolve o motivo desativado às novas publicações e salva")]
    public async Task Reativar_MotivoDesativado_Salva()
    {
        MotivoDeDecisao motivo = MotivoValido();
        motivo.Desativar();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await ReativarMotivoDeDecisaoCommandHandler.Handle(
            new ReativarMotivoDeDecisaoCommand(motivo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reativar motivo já ativo recusa e descarta o rastreamento sem salvar")]
    public async Task Reativar_MotivoJaAtivo_RecusaSemSalvar()
    {
        MotivoDeDecisao motivo = MotivoValido();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await ReativarMotivoDeDecisaoCommandHandler.Handle(
            new ReativarMotivoDeDecisaoCommand(motivo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaAtivo);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conflito de concorrência ao reativar descarta o rastreamento e devolve a situação alterada")]
    public async Task Reativar_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        MotivoDeDecisao motivo = MotivoValido();
        motivo.Desativar();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await ReativarMotivoDeDecisaoCommandHandler.Handle(
            new ReativarMotivoDeDecisaoCommand(motivo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Reativar motivo inexistente devolve não encontrado")]
    public async Task Reativar_MotivoInexistente_NaoEncontrado()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MotivoDeDecisao?)null);

        Result resultado = await ReativarMotivoDeDecisaoCommandHandler.Handle(
            new ReativarMotivoDeDecisaoCommand(Guid.CreateVersion7()), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Atualizar troca a descrição e salva")]
    public async Task Atualizar_DescricaoNova_Salva()
    {
        MotivoDeDecisao motivo = MotivoValido();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await AtualizarMotivoDeDecisaoCommandHandler.Handle(
            new AtualizarMotivoDeDecisaoCommand(motivo.Id, "Nova redação do motivo."),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        motivo.Descricao.Should().Be("Nova redação do motivo.");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Violação do índice único em corrida vira código já existente, com o rastreamento descartado")]
    public async Task Criar_CorridaNoIndiceUnico_DevolveCodigoJaExiste()
    {
        _repository.CodigoExisteAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Codigo, Arg.Any<CancellationToken>()).Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética", new ViolacaoDeUnicidade("ux_motivos_de_decisao_tipo_codigo")));

        Result<Guid> resultado = await CriarMotivoDeDecisaoCommandHandler.Handle(
            Comando(SentidoDoMotivoCodigo.Favoravel), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outro índice não é traduzida e propaga")]
    public async Task Criar_ViolacaoDeOutroIndice_Propaga()
    {
        _repository.CodigoExisteAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Codigo, Arg.Any<CancellationToken>()).Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética", new ViolacaoDeUnicidade("outro_indice")));

        Func<Task> criar = () => CriarMotivoDeDecisaoCommandHandler.Handle(
            Comando(SentidoDoMotivoCodigo.Favoravel), _repository, _unitOfWork, CancellationToken.None);

        await criar.Should().ThrowAsync<DbUpdateException>();
    }

    private static CriarMotivoDeDecisaoCommand Comando(string sentido) =>
        new(TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, Codigo, Descricao, FundamentoIsencaoCodigo.CadastroUnico, sentido);

    private static MotivoDeDecisao MotivoValido() =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.IsencaoDaTaxa,
            Codigo,
            Descricao,
            FundamentoIsencaoCodigo.CadastroUnico,
            SentidoDoMotivo.Desfavoravel)).Value!;

    /// <summary>Imita a exceção do provedor, que expõe o SQLSTATE e o nome do índice violado.</summary>
    private sealed class ViolacaoDeUnicidade(string constraintName) : Exception("violação de unicidade sintética")
    {
        public string SqlState { get; } = "23505";

        public string ConstraintName => constraintName;
    }
}
