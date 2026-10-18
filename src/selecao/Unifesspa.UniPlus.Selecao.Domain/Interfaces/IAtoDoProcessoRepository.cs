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

    /// <summary>
    /// O processo a que o ato pertence, segundo o registro da Seleção. Nulo quando o ato não tem
    /// registro — o de outro domínio, ou um id que não é de ato.
    /// </summary>
    Task<Guid?> ObterProcessoDoAtoAsync(Guid atoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os atos da cadeia de correção que termina em <paramref name="atoId"/>, do próprio ato à raiz.
    /// Vazio quando <paramref name="atoId"/> não tem registro. A cadeia é linear, então a leitura
    /// segue o ato corrigido de cada um até o que não corrige ninguém.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarCadeiaDeCorrecaoAsync(Guid atoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O ato que corrige <paramref name="atoCorrigidoId"/>, ainda que pendente; nulo quando nenhum o
    /// corrige. Só o registro da Seleção enxerga o ato pendente: o Publicações ainda não o conhece.
    /// </summary>
    Task<Guid?> ObterAtoQueCorrigeAsync(Guid atoCorrigidoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dos documentos pedidos, os que o processo já usou como principal ou como anexo de algum ato.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListarDocumentosJaUsadosAsync(
        Guid processoSeletivoId,
        IReadOnlyCollection<Guid> documentoIds,
        CancellationToken cancellationToken = default);
}
