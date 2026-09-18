namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da vitrine pública: uma página da tabela de divulgações, ordenada por urgência.
/// </summary>
/// <remarks>
/// <para>
/// Só existe linha para certame público, então a página sai do banco com o tamanho pedido e não há
/// descarte depois, nem pergunta a outro módulo no caminho da requisição. A página e os contadores
/// vêm da mesma leitura, porque descrevem o mesmo conjunto.
/// </para>
/// <para>
/// Um item cuja divulgação não se deserializa é omitido, não derruba a lista: o defeito de uma
/// linha não é culpa dos outros certames, e ele aflora no detalhe, que recusa.
/// </para>
/// <para>
/// A situação de cada item é classificada contra o instante que a consulta congelou e o mesmo
/// limiar: reclassificar contra o relógio faria o item discordar do grupo em que foi listado.
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

    /// <summary>
    /// A ordem que vale quando a consulta não pede outra: vazia, porque a rotação por urgência não
    /// é composta de campos do catálogo — ela mistura o segmento aberto/encerrado, que depende do
    /// instante da consulta e não é atributo de linha nenhuma, com o prazo. Quem a monta é a
    /// consulta, que tem o instante congelado em mãos.
    /// </summary>
    private static readonly IReadOnlyList<SortField> OrdemCanonica = [];

    public static async Task<Result<ListarCertamesPublicadosResult>> Handle(
        ListarCertamesPublicadosQuery query,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        Result<string?> busca = BuscaPedida.Validar(query.Recorte.Busca);
        if (!busca.IsSuccess)
        {
            return Result<ListarCertamesPublicadosResult>.Failure(busca.Error!);
        }

        // Campo fora do catálogo é recusado nomeando o campo e os aceitos. Numa rota anônima,
        // traduzir o desconhecido em ordem arbitrária seria oferecer consulta livre sobre o banco;
        // ignorá-lo em silêncio seria deixar o cliente crer que ordenou.
        Result<IReadOnlyList<SortField>> ordenacao = OrdenacaoPedida.Resolver(
            query.Ordenacao, CamposOrdenacaoDaVitrine.Todos, OrdemCanonica);

        if (!ordenacao.IsSuccess)
        {
            return Result<ListarCertamesPublicadosResult>.Failure(ordenacao.Error!);
        }

        PaginaDaVitrine pagina = await certameDivulgadoRepository
            .ListarVitrineAsync(
                query.Instante, query.Recorte, ordenacao.Value!, LimiarDosUltimosDias,
                query.AfterSortKey, query.AfterId, query.Limit, query.Direction,
                query.IncluirContadores, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset instante = pagina.InstanteEfetivo;

        List<CertameNaVitrineDto> itens = [];
        foreach (CertameDivulgado divulgado in pagina.Itens)
        {
            if (!ProjecaoDoCertamePublicado.TentarLerProjecao(divulgado.Certame, out CertamePublicadoDto? certame))
            {
                continue;
            }

            itens.Add(new CertameNaVitrineDto(
                divulgado.Id,
                certame.Periodo.Numero,
                certame.Nome,
                certame.TipoProcesso,
                certame.ModalidadesOfertadas,
                divulgado.InscricoesDe,
                divulgado.InscricoesAte,
                SituacaoDaVitrine.Classificar(
                    divulgado.InscricoesDe, divulgado.InscricoesAte, instante, LimiarDosUltimosDias),
                certame.Vagas.Sum(static v => v.TotalPublicado)));
        }

        return Result<ListarCertamesPublicadosResult>.Success(
            new ListarCertamesPublicadosResult(itens, pagina.Anterior, pagina.Proximo, pagina.Contadores));
    }
}
