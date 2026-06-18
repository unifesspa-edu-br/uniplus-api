namespace Unifesspa.UniPlus.Geo.Application.Queries.Cidades;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;

/// <summary>
/// Handler convention-based de <see cref="ObterCidadePorCodigoIbgeQuery"/>: lê a
/// Cidade vigente + indicador 1:1 num único snapshot e projeta em
/// <c>CidadeDetalheDto</c>; <see langword="null"/> quando o município inexiste.
/// </summary>
public static class ObterCidadePorCodigoIbgeQueryHandler
{
    public static async Task<CidadeDetalheDto?> Handle(
        ObterCidadePorCodigoIbgeQuery query,
        ICidadeReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        CidadeComIndicador? dados = await reader
            .ObterDetalhePorCodigoIbgeAsync(query.CodigoIbge, cancellationToken)
            .ConfigureAwait(false);

        // Guard explícito: município inexistente → null (404 no controller). Evita o
        // null-conditional encadeado, que a análise estática lê como possível deref nulo.
        if (dados is null)
        {
            return null;
        }

        return dados.Cidade.ToDetalheDto(dados.Indicador);
    }
}
