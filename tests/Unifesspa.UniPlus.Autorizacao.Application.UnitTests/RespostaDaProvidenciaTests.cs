namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O caso de uso da resposta: a trava compartilhada antes da leitura, a gravação e o conflito (UNI-REQ-0376).</summary>
public sealed class RespostaDaProvidenciaTests
{
    private readonly CenarioDaProvidencia _cenario = new();
    private readonly ProvidenciaBuilder _construtor = new();

    [Fact(DisplayName = "A resposta trava a concessão antes de ler a providência, grava e avisa quem solicitou")]
    public async Task Responder_TravaLeGravaEAvisa()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await ResponderAsync(providencia.Id);

        (resposta.Value!.Situacao, resposta.Value.Desfecho!.Motivo, mensagens.Count()).Should().Be(("respondida", ProvidenciaBuilder.Motivo, 1));
        Received.InOrder(() =>
        {
            _ = _cenario.Providencias.ObterConcessaoIdAsync(providencia.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Providencias.ObterConcessaoSobTravaCompartilhadaAsync(providencia.ConcessaoId, Arg.Any<CancellationToken>());
            _ = _cenario.Providencias.ObterPorIdAsync(providencia.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A gravação concorrente da mesma providência recebe o conflito de concorrência, descarta o rastreamento e não avisa")]
    public async Task Responder_ConflitoNaGravacao()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());
        _cenario.Unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await ResponderAsync(providencia.Id);

        (resposta.Error!.Code, mensagens.Count()).Should().Be((ProvidenciaErrorCodes.ConflitoDeConcorrencia, 0));
        _cenario.Unidade.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A recusa do agregado descarta o rastreamento e não grava nem avisa")]
    public async Task Responder_ConcessaoRevogada_NaoGrava()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());
        _construtor.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(), "Revogada pela revogação comum.");

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await ResponderAsync(providencia.Id);

        (resposta.Error!.Code, mensagens.Count(), providencia.Estado).Should().Be((ProvidenciaErrorCodes.ConcessaoNaoAtiva, 0, EstadoDaProvidencia.Aberta));
        await _cenario.Unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.Unidade.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A providência inexistente responde não encontrada, sem travar a concessão")]
    public async Task Responder_Inexistente_NaoEncontrada()
    {
        (Result<ProvidenciaDto> resposta, _) = await ResponderAsync(Guid.CreateVersion7());

        resposta.Error!.Code.Should().Be(ProvidenciaErrorCodes.NaoEncontrada);
        await _cenario.Providencias.DidNotReceiveWithAnyArgs().ObterConcessaoSobTravaCompartilhadaAsync(default);
    }

    [Fact(DisplayName = "Sem emissor e sub no token, a resposta é recusada antes de ler a providência")]
    public async Task Responder_AutorNaoIdentificado()
    {
        (Result<ProvidenciaDto> resposta, _) = await ProvidenciaCommandHandler.Handle(
            new ResponderProvidenciaCommand(Guid.CreateVersion7(), ProvidenciaBuilder.Motivo), _cenario.Resposta, Substitute.For<IUserContext>(), CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProvidenciaErrorCodes.AutorNaoIdentificado);
        await _cenario.Providencias.DidNotReceiveWithAnyArgs().ObterConcessaoIdAsync(default);
    }

    private Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> ResponderAsync(Guid providenciaId) =>
        ProvidenciaCommandHandler.Handle(
            new ResponderProvidenciaCommand(providenciaId, ProvidenciaBuilder.Motivo), _cenario.Resposta, Usuarios.De(ProvidenciaBuilder.Diogo), CancellationToken.None);
}
