namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>
/// Repositório de <see cref="AtoDoProcesso"/> — independente de <see cref="IProcessoSeletivoRepository"/>
/// porque o registro do ato não é filho do agregado <see cref="ProcessoSeletivo"/>: tem ciclo de vida
/// próprio e é identificado pelo ato.
/// </summary>
public interface IAtoDoProcessoRepository
{
    Task AdicionarAsync(AtoDoProcesso ato, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registro identificado pelo ato, rastreado para mutação. Nulo quando o ato não é de um processo
    /// que a Seleção registrou — o registro central serve outros domínios.
    /// </summary>
    Task<AtoDoProcesso?> ObterParaMutacaoAsync(Guid atoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registro identificado pelo ato, com os anexos, sem rastreamento. Nulo quando o ato não é de um
    /// processo que a Seleção registrou.
    /// </summary>
    Task<AtoDoProcesso?> ObterComAnexosAsync(Guid atoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registros do processo, com os anexos, sem rastreamento, do mais recente para o mais antigo.
    /// </summary>
    /// <remarks>
    /// A ordem é o instante em que o registro foi gravado, com o ato como desempate: é o relógio do
    /// sistema, e não a data que o documento declara, que não é única nem confiável para ordenar.
    /// </remarks>
    Task<IReadOnlyList<AtoDoProcesso>> ListarDoProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default);
}
