namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Uma página da vitrine e, quando pedidos, os contadores por situação.
/// </summary>
/// <remarks>
/// Os dois saem juntos porque descrevem o mesmo conjunto: o número ao lado do filtro promete
/// quantos itens aquele filtro traz, e lê-lo de um estado do banco diferente do que produziu a
/// página é como a promessa passa a contradizer a tela.
/// </remarks>
/// <param name="Contadores">Nulo quando a consulta não os pediu — contar é percurso a mais.</param>
/// <param name="Revisao">
/// Marcador opaco dos certames do recorte e do que cada um tem de ordenável e exibível (versão
/// divulgada e situação no instante da travessia): igual em todas as páginas da mesma travessia,
/// diferente quando uma publicação, uma retificação ou o relógio muda esse conjunto. Não depende da
/// ordenação pedida, que a assinatura do cursor já fixa.
/// </param>
public sealed record PaginaDaVitrine(
    IReadOnlyList<CertameDivulgado> Itens,
    DateTimeOffset InstanteEfetivo,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo,
    ContadoresDaVitrine? Contadores,
    string Revisao);

/// <summary>
/// Leitura e escrita da projeção pública do certame — a tabela cuja existência de linha é a
/// publicidade.
/// </summary>
public interface ICertameDivulgadoRepository
{
    /// <summary>Divulgação corrente do processo, rastreada para mutação. Nula quando não é público.</summary>
    Task<CertameDivulgado?> ObterParaMutacaoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>Divulgação corrente do processo, para leitura. Nula quando não é público.</summary>
    Task<CertameDivulgado?> ObterParaLeituraAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Divulgação corrente localizada pelo identificador legível congelado, para leitura. Nula
    /// quando nenhum certame público o traz.
    /// </summary>
    Task<CertameDivulgado?> ObterParaLeituraPorIdentificadorAsync(
        string identificadorLegivel,
        CancellationToken cancellationToken = default);

    Task AdicionarAsync(CertameDivulgado divulgado, CancellationToken cancellationToken = default);

    /// <summary>Lista os certames publicados na ordem pedida.</summary>
    Task<PaginaDaVitrine> ListarVitrineAsync(
        ConsultaDosCertamesPublicados consulta, string? afterSortKey, Guid? afterId, int limit,
        PaginationDirection direction, CancellationToken cancellationToken = default);
}
