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
}
