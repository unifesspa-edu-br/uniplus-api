namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Refit;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

/// <summary>
/// Consulta de vínculos de discentes na API do SIGAA.
/// </summary>
/// <remarks>
/// <para>
/// A origem recusa com erro de requisição qualquer parâmetro que não conheça, em vez de
/// ignorá-lo. Por isso esta interface expõe exatamente os cinco que ela aceita — nível,
/// ano de ingresso mínimo, situação, tamanho de página e página — e nada além disso.
/// Acrescentar um sexto parâmetro aqui quebra toda chamada, não só a que o usa.
/// </para>
/// <para>
/// A situação aceita vários valores de uma vez, o que permite pedir num único acesso
/// todos os status que caracterizam vínculo em andamento.
/// </para>
/// </remarks>
internal interface ISigaaVinculoDiscenteApi
{
    /// <param name="nivel">Nível de ensino; a sincronização pede graduação.</param>
    /// <param name="anoIngressoMinimo">
    /// Ano de ingresso a partir do qual os vínculos interessam. A origem só aceita esse
    /// filtro na forma de limite inferior.
    /// </param>
    /// <param name="situacoes">
    /// Identificadores de situação acadêmica. Vazio ou nulo não filtra por situação.
    /// </param>
    /// <param name="itensPorPagina">Tamanho da página, limitado pelo teto da origem.</param>
    /// <param name="pagina">Página desejada, começando em um.</param>
    [Get("/api/vinculo_discentes")]
    Task<ColecaoHydra<VinculoDiscentePayload>> ObterVinculosAsync(
        [AliasAs("nivel")] string nivel,
        [AliasAs("anoIngresso[gte]")] int? anoIngressoMinimo,
        [AliasAs("status.id")] IEnumerable<int>? situacoes,
        [AliasAs("itensPorPagina")] int itensPorPagina,
        [AliasAs("page")] int pagina,
        CancellationToken cancellationToken = default);
}
