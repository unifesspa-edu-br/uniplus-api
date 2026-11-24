namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
    /// A ordem que vale quando a consulta não pede outra: vazia, porque a rotação por urgência não
    /// é composta de campos do catálogo — ela mistura o segmento aberto/encerrado, que depende do
    /// instante da consulta e não é atributo de linha nenhuma, com o prazo. Quem a monta é a
    /// consulta, que tem o instante congelado em mãos.
    /// </summary>
    private static readonly IReadOnlyList<SortField> OrdemCanonica = [];

    // Campo fora do catálogo é recusado nomeando o campo e os aceitos. Numa rota anônima, traduzir o desconhecido
    // em ordem arbitrária seria oferecer consulta livre sobre o banco; ignorá-lo em silêncio seria deixar o
    // cliente crer que ordenou.
    public static async Task<Result<ListarCertamesPublicadosResult>> Handle(
        ListarCertamesPublicadosQuery query, ICertameDivulgadoRepository certameDivulgadoRepository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Result<string?> busca = BuscaPedida.Validar(query.Recorte.Busca);
        Result<IReadOnlyList<SortField>> ordenacao = OrdenacaoPedida.Resolver(query.Ordenacao, CamposOrdenacaoDaVitrine.Todos, OrdemCanonica);

        if (!busca.IsSuccess || !ordenacao.IsSuccess)
        {
            return Result<ListarCertamesPublicadosResult>.Failure((busca.IsSuccess ? ordenacao.Error : busca.Error)!);
        }

        PaginaDaVitrine pagina = await ListarPaginaAsync(query, certameDivulgadoRepository, ordenacao.Value!, cancellationToken).ConfigureAwait(false);
        return Result<ListarCertamesPublicadosResult>.Success(
            new ListarCertamesPublicadosResult(ItensDaVitrine(pagina), pagina.Anterior, pagina.Proximo, pagina.Contadores, pagina.Revisao));
    }

    private static Task<PaginaDaVitrine> ListarPaginaAsync(
        ListarCertamesPublicadosQuery query, ICertameDivulgadoRepository certameDivulgadoRepository,
        IReadOnlyList<SortField> ordenacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        return certameDivulgadoRepository.ListarVitrineAsync(
            query.Instante, query.Recorte, ordenacao, SituacaoDaVitrine.LimiarDosUltimosDias,
            query.AfterSortKey, query.AfterId, query.Limit, query.Direction,
            query.IncluirContadores, ProjecaoDoCertamePublicado.Versao, cancellationToken);
    }

    private static List<CertameNaVitrineDto> ItensDaVitrine(PaginaDaVitrine pagina)
    {
        List<CertameNaVitrineDto> itens = [];
        foreach (CertameDivulgado divulgado in pagina.Itens)
        {
            if (LeituraDoDocumentoDivulgado.TentarLerProjecao(divulgado.DocumentoPublico, out CertamePublicadoDto? certame))
            {
                itens.Add(ItemDaVitrine(divulgado, certame, pagina.InstanteEfetivo));
            }
        }

        return itens;
    }

    private static CertameNaVitrineDto ItemDaVitrine(CertameDivulgado divulgado, CertamePublicadoDto certame, DateTimeOffset instante) => new(
        divulgado.Id,
        certame.IdentificadorLegivel,
        certame.Periodo.Numero,
        certame.Nome,
        certame.TipoProcesso,
        certame.ModalidadesOfertadas,
        divulgado.InscricoesDe,
        divulgado.InscricoesAte,
        CodigoComRotulo<SituacaoDoCertame>.De(SituacaoDaVitrine.Classificar(divulgado.InscricoesDe, divulgado.InscricoesAte, instante, SituacaoDaVitrine.LimiarDosUltimosDias)),
        certame.Vagas.Sum(static v => v.TotalDeVagasDoEdital));
}
