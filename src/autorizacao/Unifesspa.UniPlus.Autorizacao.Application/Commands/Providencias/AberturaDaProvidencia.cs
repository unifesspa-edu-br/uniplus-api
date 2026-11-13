namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A abertura da providência (UNI-REQ-0375). Toma a trava compartilhada da concessão antes de ler a providência aberta e o
/// instante: a revogação concorrente, por qualquer caminho, espera ou é esperada, e a abertura nunca é gravada sobre uma
/// concessão já revogada (ADR-0164).
/// </summary>
public sealed class AberturaDaProvidencia
{
    private readonly IProvidenciaRepository _providencias;
    private readonly GravacaoDaProvidencia _gravacao;
    private readonly TimeProvider _clock;

    public AberturaDaProvidencia(IProvidenciaRepository providencias, GravacaoDaProvidencia gravacao, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(providencias);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(clock);

        _providencias = providencias;
        _gravacao = gravacao;
        _clock = clock;
    }

    internal async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> AbrirAsync(
        SolicitarProvidenciaCommand command, PessoaNoToken solicitante, CancellationToken cancellationToken)
    {
        if (await _providencias.ObterConcessaoSobTravaCompartilhadaAsync(command.ConcessaoId, cancellationToken).ConfigureAwait(false) is not { } alvo)
        {
            return (Result<ProvidenciaDto>.Failure(ConcessaoHandlerErros.NaoEncontrada(command.ConcessaoId)), []);
        }

        Providencia? jaAberta = await _providencias.ObterAbertaDaConcessaoAsync(command.ConcessaoId, cancellationToken).ConfigureAwait(false);
        AtoDoAgente ato = new(solicitante.Identidade, _clock.GetUtcNow());
        Result<Providencia> aberta = Providencia.Abrir(alvo.Concessao, jaAberta, command.Solicitacao, solicitante, ato.Instante);

        return aberta.IsFailure
            ? (Result<ProvidenciaDto>.ValidationFailure(aberta.Errors), [])
            : await GravarAsync(new ProvidenciaComConcessao(aberta.Value!, alvo), ato, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> GravarAsync(
        ProvidenciaComConcessao nova, AtoDoAgente ato, CancellationToken cancellationToken)
    {
        await _providencias.AdicionarAsync(nova.Providencia, cancellationToken).ConfigureAwait(false);

        return await _gravacao.GravarAsync(Result.Success(), nova, ato, cancellationToken).ConfigureAwait(false);
    }
}
