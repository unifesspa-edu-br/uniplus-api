namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using Refit;

/// <summary>
/// Consulta unificada de atos por entidade no módulo Publicações (anônima), paginada por cursor.
/// </summary>
internal interface IPublicacoesAtosApi
{
    /// <param name="processoSeletivoId">Entidade cujos atos se pede.</param>
    /// <param name="cursor">Cursor opaco da página seguinte; nulo na primeira página.</param>
    /// <param name="limite">
    /// Tamanho da página. Só vai na primeira requisição: o cursor já carrega a janela.
    /// </param>
    [Get("/api/publicacoes/entidades/PROCESSO_SELETIVO/{processoSeletivoId}/atos")]
    [Headers("Accept: application/vnd.uniplus.ato-normativo.v1+json")]
    Task<IApiResponse<List<AtoNormativoPayload>>> ListarAsync(
        Guid processoSeletivoId,
        [AliasAs("cursor")] string? cursor,
        [AliasAs("limit")] int? limite,
        CancellationToken cancellationToken);
}
