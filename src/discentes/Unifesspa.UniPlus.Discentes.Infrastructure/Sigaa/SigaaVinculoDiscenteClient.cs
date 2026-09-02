namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;
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

        return new PaginaDeVinculos(
            colecao.Itens,
            colecao.TotalDeItens,
            TemProximaPagina(colecao));
    }

    /// <summary>
    /// Decide se há continuação sem depender do total declarado.
    /// </summary>
    /// <remarks>
    /// A indicação de próxima página que a origem envia é a informação mais confiável,
    /// porque é calculada no mesmo instante da resposta. Quando ela não vem, o critério é
    /// a página ter vindo cheia: página incompleta é a última, e página vazia encerra.
    /// </remarks>
    private bool TemProximaPagina(ColecaoHydra<VinculoDiscentePayload> colecao) =>
        colecao.Visao?.Proxima is { Length: > 0 }
        || (colecao.Visao is null && colecao.Itens.Count >= _opcoes.ItensPorPagina);
}
