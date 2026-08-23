namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handlers do catálogo de motivos de decisão de isenção: unicidade do código,
/// conversão dos códigos do wire e ciclo de vida.
/// </summary>
public sealed class MotivoDecisaoIsencaoCommandHandlersTests
{
    private const string Codigo = "RENDA_ACIMA_DO_LIMITE";
    private const string Descricao = "Renda familiar per capita acima do limite legal.";

    private readonly IMotivoDecisaoIsencaoRepository _repository =
        Substitute.For<IMotivoDecisaoIsencaoRepository>();

    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    [Fact(DisplayName = "Criar converte os códigos do wire e persiste o motivo")]
    public async Task Criar_PayloadValido_Persiste()
    {
        _repository.CodigoExisteAsync(Codigo, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarMotivoDecisaoIsencaoCommandHandler.Handle(
            new CriarMotivoDecisaoIsencaoCommand(
                Codigo,
                Descricao,
                FundamentoIsencaoCodigo.CadastroUnico,
                ResultadoPermitidoCodigo.Indeferido),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<MotivoDecisaoIsencao>(motivo =>
                motivo.Fundamento == FundamentoIsencao.CadastroUnico
                && motivo.ResultadoPermitido == ResultadoPermitido.Indeferido),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Criar recusa código já usado sem tocar no repositório")]
    public async Task Criar_CodigoJaExiste_Recusa()
    {
        _repository.CodigoExisteAsync(Codigo, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarMotivoDecisaoIsencaoCommandHandler.Handle(
            new CriarMotivoDecisaoIsencaoCommand(
                Codigo,
                Descricao,
                FundamentoIsencaoCodigo.CadastroUnico,
                ResultadoPermitidoCodigo.Indeferido),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(
            Arg.Any<MotivoDecisaoIsencao>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resultado permitido fora do vocabulário do wire é recusado como ausente")]
    public async Task Criar_ResultadoForaDoVocabulario_Recusa()
    {
        Result<Guid> resultado = await CriarMotivoDecisaoIsencaoCommandHandler.Handle(
            new CriarMotivoDecisaoIsencaoCommand(Codigo, Descricao, FundamentoIsencaoCodigo.CadastroUnico, "TALVEZ"),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.ResultadoPermitidoObrigatorio);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desativar aplica a desativação e salva")]
    public async Task Desativar_MotivoAtivo_Salva()
    {
        MotivoDecisaoIsencao motivo = MotivoValido();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await DesativarMotivoDecisaoIsencaoCommandHandler.Handle(
            new DesativarMotivoDecisaoIsencaoCommand(motivo.Id),
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
            .Returns((MotivoDecisaoIsencao?)null);

        Result resultado = await DesativarMotivoDecisaoIsencaoCommandHandler.Handle(
            new DesativarMotivoDecisaoIsencaoCommand(Guid.CreateVersion7()),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Recusa do agregado descarta o rastreamento antes de devolver a falha")]
    public async Task Desativar_MotivoJaInativo_DescartaRastreamento()
    {
        MotivoDecisaoIsencao motivo = MotivoValido();
        motivo.Desativar();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await DesativarMotivoDecisaoIsencaoCommandHandler.Handle(
            new DesativarMotivoDecisaoIsencaoCommand(motivo.Id),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.JaInativo);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualizar troca a descrição e salva")]
    public async Task Atualizar_DescricaoNova_Salva()
    {
        MotivoDecisaoIsencao motivo = MotivoValido();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result resultado = await AtualizarMotivoDecisaoIsencaoCommandHandler.Handle(
            new AtualizarMotivoDecisaoIsencaoCommand(motivo.Id, "Nova redação do motivo."),
            _repository,
            _unitOfWork,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        motivo.Descricao.Should().Be("Nova redação do motivo.");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static MotivoDecisaoIsencao MotivoValido() =>
        MotivoDecisaoIsencao.Criar(
            Codigo,
            Descricao,
            FundamentoIsencao.CadastroUnico,
            ResultadoPermitido.Indeferido).Value!;
}
