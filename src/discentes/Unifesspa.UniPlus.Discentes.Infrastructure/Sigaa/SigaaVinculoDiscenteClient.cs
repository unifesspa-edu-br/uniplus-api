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
/// Traduz o recorte pedido para os parâmetros que a origem aceita e devolve a página
/// junto com o que se sabe sobre a continuação — sem interpretar o conteúdo dos
/// vínculos, que é assunto de quem os converte para o domínio.
/// </remarks>
internal sealed class SigaaVinculoDiscenteClient : ISigaaVinculoDiscenteClient
{
    private readonly ISigaaVinculoDiscenteApi _api;
    private readonly SigaaOptions _opcoes;

    public SigaaVinculoDiscenteClient(ISigaaVinculoDiscenteApi api, IOptions<SigaaOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(opcoes);

        _api = api;
        _opcoes = opcoes.Value;
    }

    public async Task<PaginaDeVinculos> ObterPaginaAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentOutOfRangeException.ThrowIfLessThan(pagina, 1);

        ColecaoHydra<VinculoDiscentePayload> colecao = await _api
            .ObterVinculosAsync(
                filtro.Nivel,
                filtro.AnoIngressoMinimo,
                filtro.Situacoes is { Count: > 0 } ? filtro.Situacoes : null,
                _opcoes.ItensPorPagina,
                pagina,
                cancellationToken)
            .ConfigureAwait(false);

        if (colecao.Itens is not { } itens)
        {
            throw new EnvelopeDaOrigemInvalidoException(
                "A resposta do SIGAA não trouxe a propriedade \"hydra:member\". Uma página sem "
                + "essa propriedade não é uma página vazia: é envelope fora do contrato, e "
                + "aceitá-la encerraria a varredura como bem-sucedida sem ler vínculo nenhum.");
        }

        return new PaginaDeVinculos(
            itens,
            colecao.TotalDeItens,
            TemProximaPagina(colecao.Visao, itens.Count));
    }

    public async IAsyncEnumerable<PaginaDeVinculos> PercorrerAsync(
        FiltroDeVinculos filtro,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        PaginaDeVinculos ultima = await ObterPaginaAsync(filtro, 1, cancellationToken)
            .ConfigureAwait(false);

        yield return ultima;

        int numeroDaUltima = 1;

        // O total só dimensiona o trabalho: sabendo quantas páginas existem, dá para
        // buscá-las em paralelo em vez de uma de cada vez.
        if (ultima.TotalDeItensNaOrigem is { } total)
        {
            int previstas = (int)Math.Ceiling(total / (double)_opcoes.ItensPorPagina);

            for (int inicio = 2; inicio <= previstas; inicio += _opcoes.GrauDeParalelismo)
            {
                int fim = Math.Min(inicio + _opcoes.GrauDeParalelismo - 1, previstas);

                Task<PaginaDeVinculos>[] emVoo =
                [
                    .. Enumerable.Range(inicio, fim - inicio + 1)
                        .Select(numero => ObterPaginaAsync(filtro, numero, cancellationToken)),
                ];

                PaginaDeVinculos[] doBloco = await Task.WhenAll(emVoo).ConfigureAwait(false);

                foreach (PaginaDeVinculos pagina in doBloco)
                {
                    yield return pagina;
                }

                ultima = doBloco[^1];
                numeroDaUltima = fim;
            }
        }

        // Quem decide o fim é a origem, não a conta feita no início. Vínculos criados
        // durante a varredura empurram o fim para além do previsto, e parar na estimativa
        // os deixaria de fora — sem erro algum, com a execução registrada como completa.
        while (ultima.TemProximaPagina)
        {
            numeroDaUltima++;
            ultima = await ObterPaginaAsync(filtro, numeroDaUltima, cancellationToken)
                .ConfigureAwait(false);

            yield return ultima;
        }
    }

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
        || (visao is null && itensNaPagina >= _opcoes.ItensPorPagina);
}
