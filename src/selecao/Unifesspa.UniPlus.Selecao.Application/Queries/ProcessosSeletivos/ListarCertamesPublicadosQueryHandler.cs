namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

/// <summary>
/// Handler da vitrine pública: uma página da tabela de divulgações, ordenada por urgência.
/// </summary>
/// <remarks>
/// <para>
/// Só existe linha para certame público, então a página sai do banco com o tamanho pedido e não há
/// descarte depois. Some a ressalva de página curta, some a advertência de que página vazia não
/// significa fim de coleção, e some a pergunta a outro módulo no caminho da requisição.
/// </para>
/// <para>
/// Um item cuja divulgação não se deserializa é omitido, não derruba a lista: o defeito de uma
/// linha não é culpa dos outros certames, e ele aflora no detalhe, que recusa.
/// </para>
/// </remarks>
public static class ListarCertamesPublicadosQueryHandler
{
    /// <summary>
    /// A partir de quanto tempo do encerramento um certame é "últimos dias".
    /// </summary>
    /// <remarks>
    /// <b>Valor provisório.</b> O estado aparece no modelo de interface do portal, mas o limiar que o
    /// define não está declarado em requisito nem em regra de negócio publicada — e ele é decisão de
    /// produto, não de implementação: encurtá-lo ou alargá-lo muda o que o cidadão lê como urgente.
    /// Sete dias é o que se assume até a regra existir, e trocá-lo é mudar esta constante.
    /// </remarks>
    private static readonly TimeSpan LimiarDosUltimosDias = TimeSpan.FromDays(7);

    public static async Task<ListarCertamesPublicadosResult> Handle(
        ListarCertamesPublicadosQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        (IReadOnlyList<CertameDivulgado> divulgados, DateTimeOffset instante, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await certameDivulgadoRepository
                .ListarVitrineAsync(
                    query.Instante, query.Situacao, LimiarDosUltimosDias, query.AfterSortKey, query.AfterId,
                    query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);

        ContadoresDaVitrine? contadores = query.IncluirContadores
            ? await certameDivulgadoRepository
                .ContarPorSituacaoAsync(instante, LimiarDosUltimosDias, cancellationToken)
                .ConfigureAwait(false)
            : null;

        List<CertameNaVitrineDto> itens = [];
        foreach (CertameDivulgado divulgado in divulgados)
        {
            if (JsonSerializer.Deserialize<CertamePublicadoDto>(divulgado.Certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento) is not { } certame)
            {
                continue;
            }

            itens.Add(new CertameNaVitrineDto(
                divulgado.Id,
                certame.Periodo.Numero,
                certame.TipoProcesso.Nome,
                certame.TipoProcesso,
                certame.ModalidadesOfertadas,
                divulgado.InscricoesAte,
                divulgado.InscricoesDe <= instante && divulgado.InscricoesAte >= instante,
                certame.Vagas.Sum(static v => v.TotalPublicado)));
        }

        return new ListarCertamesPublicadosResult(itens, anterior, proximo, contadores);
    }
}
