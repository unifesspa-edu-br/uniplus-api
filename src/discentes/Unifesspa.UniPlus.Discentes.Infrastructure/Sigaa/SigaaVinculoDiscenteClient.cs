namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

/// <summary>
/// Busca vínculos de discentes na API do SIGAA.
/// </summary>
/// <remarks>
/// Traduz o filtro pedido para os parâmetros que a origem aceita e devolve a página
/// junto com o que se sabe sobre a continuação — sem interpretar o conteúdo dos
/// vínculos, que é assunto de quem os converte para o domínio.
/// </remarks>
internal sealed class SigaaVinculoDiscenteClient : ISigaaVinculoDiscenteClient
{
    private readonly ISigaaVinculoDiscenteApi _api;
    private readonly PaginacaoDoSigaa _paginacao;

    public SigaaVinculoDiscenteClient(ISigaaVinculoDiscenteApi api, IOptions<SigaaOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(opcoes);

        _api = api;
        _paginacao = opcoes.Value.Paginacao;
    }

    public async Task<PaginaDeVinculos> ObterPaginaAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentOutOfRangeException.ThrowIfLessThan(pagina, 1);

        ColecaoHydra<VinculoDiscentePayload> colecao = await PedirAsync(filtro, pagina, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<VinculoDiscentePayload> itens = ItensDe(colecao);

        return new PaginaDeVinculos(itens, colecao.TotalDeItens, TemProximaPagina(colecao.Visao, itens.Count));
    }

    public async IAsyncEnumerable<PaginaDeVinculos> PercorrerAsync(
        FiltroDeVinculos filtro,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        await foreach (PaginaDeVinculos[] bloco in BlocosAsync(filtro, cancellationToken).ConfigureAwait(false))
        {
            foreach (PaginaDeVinculos pagina in bloco)
            {
                yield return pagina;
            }
        }
    }

    private Task<ColecaoHydra<VinculoDiscentePayload>> PedirAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken) =>
        _api.ObterVinculosAsync(
            filtro.Nivel,
            filtro.AnoIngressoMinimo,
            filtro.Situacoes is { Count: > 0 } ? filtro.Situacoes : null,
            _paginacao.ItensPorPagina,
            pagina,
            cancellationToken);

    private static IReadOnlyList<VinculoDiscentePayload> ItensDe(ColecaoHydra<VinculoDiscentePayload> colecao)
    {
        if (colecao.Itens is not { } itens)
        {
            throw new EnvelopeDaOrigemInvalidoException(
                "A resposta do SIGAA não trouxe a propriedade \"hydra:member\". Uma página sem "
                + "essa propriedade não é uma página vazia: é envelope fora do contrato, e "
                + "aceitá-la encerraria a varredura como bem-sucedida sem ler vínculo nenhum.");
        }

        return itens;
    }

    /// <summary>
    /// As páginas da varredura, em blocos: a primeira sozinha, depois as que o total
    /// declarado permite buscar em paralelo e, por fim, uma de cada vez enquanto a origem
    /// indicar continuação.
    /// </summary>
    private async IAsyncEnumerable<PaginaDeVinculos[]> BlocosAsync(
        FiltroDeVinculos filtro,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        PaginaDeVinculos primeira = await ObterPaginaAsync(filtro, 1, cancellationToken).ConfigureAwait(false);
        PaginaDeVinculos[] bloco = [primeira];
        int previstas = PaginasPrevistas(primeira);
        int numeroDaUltima = 1;

        while (bloco.Length > 0)
        {
            yield return bloco;

            bloco = await ProximoBlocoAsync(filtro, numeroDaUltima, previstas, bloco[^1], cancellationToken)
                .ConfigureAwait(false);
            numeroDaUltima += bloco.Length;
        }
    }

    /// <summary>
    /// O total só dimensiona o trabalho: sabendo quantas páginas existem, dá para buscá-las
    /// em paralelo em vez de uma de cada vez.
    /// </summary>
    private int PaginasPrevistas(PaginaDeVinculos primeira) =>
        primeira.TotalDeItensNaOrigem is { } total
            ? (int)Math.Ceiling(total / (double)_paginacao.ItensPorPagina)
            : 0;

    /// <remarks>
    /// Quem decide o fim é a origem, não a conta feita no início. Vínculos criados durante
    /// a varredura empurram o fim para além do previsto, e parar na estimativa os deixaria
    /// de fora — sem erro algum, com a execução registrada como completa.
    /// </remarks>
    private async Task<PaginaDeVinculos[]> ProximoBlocoAsync(
        FiltroDeVinculos filtro,
        int numeroDaUltima,
        int previstas,
        PaginaDeVinculos ultima,
        CancellationToken cancellationToken)
    {
        if (numeroDaUltima < previstas)
        {
            int fim = Math.Min(numeroDaUltima + _paginacao.GrauDeParalelismo, previstas);
            return await ObterEmParaleloAsync(filtro, numeroDaUltima + 1, fim, cancellationToken)
                .ConfigureAwait(false);
        }

        return ultima.TemProximaPagina
            ? [await ObterPaginaAsync(filtro, numeroDaUltima + 1, cancellationToken).ConfigureAwait(false)]
            : [];
    }

    private Task<PaginaDeVinculos[]> ObterEmParaleloAsync(
        FiltroDeVinculos filtro,
        int primeira,
        int ultima,
        CancellationToken cancellationToken) =>
        Task.WhenAll(
            Enumerable.Range(primeira, ultima - primeira + 1)
                .Select(numero => ObterPaginaAsync(filtro, numero, cancellationToken)));

    /// <summary>
    /// Decide se há continuação sem depender do total declarado.
    /// </summary>
    /// <remarks>
    /// A indicação de próxima página que a origem envia é a informação mais confiável,
    /// porque é calculada no mesmo instante da resposta. Quando ela não vem, o critério é
    /// a página ter vindo cheia: página incompleta é a última, e página vazia encerra.
    /// </remarks>
    private bool TemProximaPagina(VisaoHydra? visao, int itensNaPagina) =>
        visao?.Proxima is { Length: > 0 }
        || (visao is null && itensNaPagina >= _paginacao.ItensPorPagina);
}
