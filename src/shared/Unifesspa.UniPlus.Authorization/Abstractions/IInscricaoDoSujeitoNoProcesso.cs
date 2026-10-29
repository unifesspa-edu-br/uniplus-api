namespace Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Porta pela qual a decisão pergunta se a pessoa tem inscrição enviada e não cancelada em um processo seletivo
/// (impedimento do servidor inscrito — Lei nº 9.784/1999, art. 18, I). Quem guarda as inscrições implementa a porta; o
/// ponto de decisão só pergunta, sem conhecer o armazenamento nem receber dado da inscrição.
/// </summary>
public interface IInscricaoDoSujeitoNoProcesso
{
    /// <summary>
    /// Se a pessoa tem inscrição enviada e não cancelada no processo. O rascunho e a inscrição cancelada não contam.
    /// Lê o estado atual a cada chamada, sem cache entre requisições: o envio e o cancelamento valem na requisição
    /// seguinte.
    /// </summary>
    /// <param name="emissor">O emissor do token. O <c>sub</c> só identifica a pessoa dentro do emissor que o expediu.</param>
    /// <param name="subject">O <c>sub</c> do token; com o emissor, a identidade que a inscrição guarda do candidato.</param>
    /// <param name="processoId">O processo seletivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> TemInscricaoEnviadaAsync(string emissor, string subject, Guid processoId, CancellationToken cancellationToken);
}
