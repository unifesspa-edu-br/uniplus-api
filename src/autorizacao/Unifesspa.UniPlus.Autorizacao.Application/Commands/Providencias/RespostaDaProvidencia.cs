namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A resposta motivada (UNI-REQ-0376). Trava a concessão com a trava compartilhada e só depois lê a providência e o
/// instante: a revogação já confirmada por outro caminho é vista, e a resposta é recusada; a que vem depois espera esta
/// terminar. Duas respostas simultâneas passam pela trava compartilhada e disputam a linha da providência pelo controle
/// otimista: uma grava, e a outra recebe o conflito de concorrência (ADR-0164, ADR-0119).
/// </summary>
public sealed class RespostaDaProvidencia
{
    private readonly IProvidenciaRepository _providencias;
    private readonly GravacaoDaProvidencia _gravacao;
    private readonly TimeProvider _clock;

    public RespostaDaProvidencia(IProvidenciaRepository providencias, GravacaoDaProvidencia gravacao, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(providencias);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(clock);

        _providencias = providencias;
        _gravacao = gravacao;
        _clock = clock;
    }

    internal async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> ResponderAsync(
        ResponderProvidenciaCommand command, Identidade quemResponde, CancellationToken cancellationToken)
    {
        if (await LerSobATravaAsync(command.ProvidenciaId, cancellationToken).ConfigureAwait(false) is not { } lida)
        {
            return (Result<ProvidenciaDto>.Failure(ProvidenciaHandlerErros.NaoEncontrada()), []);
        }

        AtoDoAgente ato = new(quemResponde, _clock.GetUtcNow());
        Result respondida = lida.Providencia.Responder(lida.Alvo.Concessao, ato, command.Motivo);

        return await _gravacao.GravarAsync(respondida, lida, ato, cancellationToken).ConfigureAwait(false);
    }

    // A concessão de uma providência não muda: a leitura sem trava basta para saber que concessão travar.
    private async Task<ProvidenciaComConcessao?> LerSobATravaAsync(Guid providenciaId, CancellationToken cancellationToken)
    {
        if (await _providencias.ObterConcessaoIdAsync(providenciaId, cancellationToken).ConfigureAwait(false) is not { } concessaoId)
        {
            return null;
        }

        ConcessaoDaProvidencia? alvo = await _providencias.ObterConcessaoSobTravaCompartilhadaAsync(concessaoId, cancellationToken).ConfigureAwait(false);
        Providencia? providencia = await _providencias.ObterPorIdAsync(providenciaId, cancellationToken).ConfigureAwait(false);

        return alvo is null || providencia is null ? null : new ProvidenciaComConcessao(providencia, alvo);
    }
}
