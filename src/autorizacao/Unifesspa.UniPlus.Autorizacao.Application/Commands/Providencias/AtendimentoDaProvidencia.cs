namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O atendimento pela revogação vinculada (UNI-REQ-0376), que segue a revogação comum: as mesmas travas, na ordem da
/// ADR-0162 (papel de Representante, pessoa do beneficiário e a concessão para alterar), a conferência do e-mail do
/// beneficiário, o mínimo de dois gestores de acessos e a regra do último Representante do controlador (UNI-REQ-0394). A
/// providência é lida depois da trava da concessão, e a revogação e o desfecho são gravados juntos, ou nenhum dos dois
/// (ADR-0164).
/// </summary>
public sealed class AtendimentoDaProvidencia
{
    private readonly IProvidenciaRepository _providencias;
    private readonly IConcessaoRepository _concessoes;
    private readonly TravasDaRevogacao _travas;
    private readonly GravacaoDaProvidencia _gravacao;
    private readonly RevogacaoDaConcessao _revogacao;
    private readonly TimeProvider _clock;

    public AtendimentoDaProvidencia(
        IProvidenciaRepository providencias,
        IConcessaoRepository concessoes,
        TravasDaRevogacao travas,
        GravacaoDaProvidencia gravacao,
        RevogacaoDaConcessao revogacao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(providencias);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(travas);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(revogacao);
        ArgumentNullException.ThrowIfNull(clock);

        _providencias = providencias;
        _concessoes = concessoes;
        _travas = travas;
        _gravacao = gravacao;
        _revogacao = revogacao;
        _clock = clock;
    }

    internal async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> AtenderAsync(
        AtenderProvidenciaCommand command, Identidade quemAtende, CancellationToken cancellationToken)
    {
        if (await LerSobATravaAsync(command.ProvidenciaId, cancellationToken).ConfigureAwait(false) is not { } lida)
        {
            return (Result<ProvidenciaDto>.Failure(ProvidenciaHandlerErros.NaoEncontrada()), []);
        }

        AtoDoAgente ato = new(quemAtende, _clock.GetUtcNow());
        Result atendida = await _revogacao
            .AplicarAsync(lida.Alvo.Concessao, ato.Instante, () => lida.Providencia.Atender(lida.Alvo.Concessao, ato, command.Declaracao), cancellationToken)
            .ConfigureAwait(false);

        return await _gravacao.GravarAsync(atendida, lida, ato, cancellationToken).ConfigureAwait(false);
    }

    // A concessão e o perfil de uma providência não mudam: a leitura sem trava basta para saber o que travar.
    private async Task<ProvidenciaComConcessao?> LerSobATravaAsync(Guid providenciaId, CancellationToken cancellationToken)
    {
        if (await _providencias.ObterConcessaoIdAsync(providenciaId, cancellationToken).ConfigureAwait(false) is not { } concessaoId
            || await _concessoes.ObterComPerfilAsync(concessaoId, cancellationToken).ConfigureAwait(false) is not { } comPerfil)
        {
            return null;
        }

        Concessao? concessao = await _travas.TravarAsync(concessaoId, cancellationToken).ConfigureAwait(false);
        Providencia? providencia = await _providencias.ObterPorIdAsync(providenciaId, cancellationToken).ConfigureAwait(false);

        return concessao is null || providencia is null
            ? null
            : new ProvidenciaComConcessao(providencia, new ConcessaoDaProvidencia(concessao, comPerfil.PerfilNome));
    }
}
