namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Diz quais fases da versão publicada vigente do processo já começaram em <c>agora</c>: pela janela publicada, pelo
/// primeiro ato ou pelo primeiro registro da fase, o que vier primeiro. A fase encerrada conta como iniciada.
/// </summary>
public interface IFasesIniciadasDoProcesso
{
    /// <summary>As fases iniciadas, identificadas pela fase canônica de origem; vazio quando o processo não tem versão publicada.</summary>
    Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// As fases iniciadas no instante que o próprio leitor lê do relógio, devolvido junto. Quem decide depois de uma
    /// trava chama este método, para que o instante seja o posterior à espera.
    /// </summary>
    Task<FasesIniciadasNoInstante> ObterAgoraAsync(Guid processoId, CancellationToken cancellationToken = default);
}
