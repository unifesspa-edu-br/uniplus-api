namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using Refit;

/// <summary>
/// Consulta à API do Geo (<c>unifesspa-geo-api</c>) — <c>GET /api/cep/{cep}</c> e
/// <c>GET /api/cidades/{codigoIbge}</c>, os mesmos endpoints que o <c>uniplus-web</c> já usa
/// (<c>libs/shared-data/.../geo</c>). O <c>Accept</c> leva a vendor MIME exigida pelo Geo
/// (<c>application/vnd.uniplus.{recurso}.v1+json</c>, mesmo esquema de <c>VendorMediaTypeAttribute</c>
/// já usado em <c>uniplus-api</c>) — sem ela, o Geo responde 406.
/// </summary>
public interface IGeoApi
{
    /// <summary>Resolve o CEP (8 dígitos) em endereço estruturado. 404 quando não há cobertura.</summary>
    [Headers("Accept: application/vnd.uniplus.cep.v1+json")]
    [Get("/api/cep/{cep}")]
    Task<ApiResponse<CepResolvidoResponse>> ResolverCepAsync(string cep, CancellationToken cancellationToken = default);

    /// <summary>Detalhe da cidade pelo código IBGE (7 dígitos). 404 quando o código não existe.</summary>
    [Headers("Accept: application/vnd.uniplus.cidade.v1+json")]
    [Get("/api/cidades/{codigoIbge}")]
    Task<ApiResponse<CidadeDetalheResponse>> ObterCidadeAsync(string codigoIbge, CancellationToken cancellationToken = default);
}

/// <summary>Só os campos que a conferência usa — não o contrato completo do Geo.</summary>
public sealed record CepResolvidoResponse(string Cidade, string CodigoIbge, string Uf);

/// <summary>Só os campos que a conferência usa — não o contrato completo do Geo.</summary>
public sealed record CidadeDetalheResponse(string CodigoIbge, string Nome, string Uf);
