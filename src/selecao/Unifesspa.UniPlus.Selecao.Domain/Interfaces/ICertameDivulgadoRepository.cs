namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O que reduz a vitrine antes de ordenar e contar: a situação da janela, a modalidade e o texto
/// pesquisado.
/// </summary>
/// <remarks>
/// Os três juntos num objeto porque andam juntos: a listagem e os contadores precisam correr sobre
/// exatamente o mesmo conjunto, e os três entram na assinatura do cursor. Passá-los soltos é como
/// um deles fica para trás numa das duas pontas.
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
    /// <para>
    /// Consulta de tabela única. Só há linha para certame público, então não há filtro de
    /// visibilidade a aplicar depois — a página sai do banco com o tamanho pedido, e o percurso não
    /// depende de descarte posterior.
    /// </para>
    /// <para>
    /// <paramref name="ordenacao"/> vazia é a ordem canônica por urgência; com campos, é a que a
    /// consulta pediu. O recorte e a ordenação entram na assinatura do cursor, de modo que uma
    /// continuação só vale para a consulta que a emitiu.
    /// </para>
    /// </remarks>
    Task<(IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarVitrineAsync(
            DateTimeOffset instanteSeForAPrimeiraPagina,
            RecorteDaVitrine recorte,
            IReadOnlyList<SortField> ordenacao,
            TimeSpan limiarDosUltimosDias,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Contagem por situação, num único percurso, sobre o mesmo recorte da listagem <b>exceto</b>
    /// pela situação: são estes números que alimentam o filtro de situação, e aplicá-lo a eles
    /// deixaria todos zerados menos um.
    /// </summary>
    Task<ContadoresDaVitrine> ContarPorSituacaoAsync(
        DateTimeOffset instante,
        RecorteDaVitrine recorte,
        TimeSpan limiarDosUltimosDias,
        CancellationToken cancellationToken = default);
}
