namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Pagination;

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
    /// <paramref name="situacao"/> nula é a vitrine inteira: a ausência de recorte é a ausência do
    /// filtro, não um valor do vocabulário.
    /// </para>
    /// </remarks>
    Task<(IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarVitrineAsync(
            DateTimeOffset instanteSeForAPrimeiraPagina,
            SituacaoDoCertame? situacao,
            TimeSpan limiarDosUltimosDias,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken = default);

    /// <summary>Contagem por situação sobre o conjunto divulgado, num único percurso.</summary>
    Task<ContadoresDaVitrine> ContarPorSituacaoAsync(
        DateTimeOffset instante,
        TimeSpan limiarDosUltimosDias,
        CancellationToken cancellationToken = default);
}
