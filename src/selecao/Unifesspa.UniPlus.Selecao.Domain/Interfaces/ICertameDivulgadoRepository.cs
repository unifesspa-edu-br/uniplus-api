namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O que reduz a vitrine antes de ordenar e contar: a situação da janela, a modalidade e o texto
/// pesquisado.
/// </summary>
/// <remarks>
/// Juntos porque a listagem e os contadores precisam correr sobre o mesmo conjunto, e os três
/// entram na assinatura do cursor.
/// </remarks>
/// <param name="Situacao">
/// Recorte por situação da janela. Nulo é a vitrine inteira — sem filtro é sem parâmetro, não um
/// valor de vocabulário que certame algum tem.
/// </param>
/// <param name="Modalidade">Código da modalidade que o certame precisa ofertar. Nulo não recorta.</param>
/// <param name="Busca">Texto pesquisado no título e no número do edital. Nulo ou em branco não recorta.</param>
public sealed record RecorteDaVitrine(
    SituacaoDoCertame? Situacao = null,
    string? Modalidade = null,
    string? Busca = null);

/// <summary>
/// Uma página da vitrine e, quando pedidos, os contadores por situação.
/// </summary>
/// <remarks>
/// Os dois saem juntos porque descrevem o mesmo conjunto: o número ao lado do filtro promete
/// quantos itens aquele filtro traz, e lê-lo de um estado do banco diferente do que produziu a
/// página é como a promessa passa a contradizer a tela.
/// </remarks>
/// <param name="Contadores">Nulo quando a consulta não os pediu — contar é percurso a mais.</param>
public sealed record PaginaDaVitrine(
    IReadOnlyList<CertameDivulgado> Itens,
    DateTimeOffset InstanteEfetivo,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo,
    ContadoresDaVitrine? Contadores);

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

    Task AdicionarAsync(CertameDivulgado divulgado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Página da vitrine, ordenada por urgência: os que ainda não encerraram primeiro, do prazo mais
    /// próximo ao mais distante, e os encerrados depois.
    /// </summary>
    /// <remarks>
    /// Consulta de tabela única: só há linha para certame público, então a página sai do banco com
    /// o tamanho pedido. <paramref name="ordenacao"/> vazia é a ordem canônica por urgência. O
    /// recorte e a ordenação entram na assinatura do cursor, e uma continuação só vale para a
    /// consulta que a emitiu.
    /// </remarks>
    /// <param name="incluirContadores">
    /// Pede a contagem por situação junto da página. Ela corre sobre o mesmo recorte <b>exceto</b>
    /// pela situação — são estes números que alimentam aquele filtro, e aplicá-lo a eles deixaria
    /// todos zerados menos um.
    /// </param>
    Task<PaginaDaVitrine> ListarVitrineAsync(
        DateTimeOffset instanteSeForAPrimeiraPagina,
        RecorteDaVitrine recorte,
        IReadOnlyList<SortField> ordenacao,
        TimeSpan limiarDosUltimosDias,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        bool incluirContadores,
        CancellationToken cancellationToken = default);
}
