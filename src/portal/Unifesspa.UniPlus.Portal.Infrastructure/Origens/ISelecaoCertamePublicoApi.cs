namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using Refit;

/// <summary>
/// Contrato público de leitura do certame no módulo Seleção (anônimo).
/// </summary>
/// <remarks>
/// A resposta é pedida como <see cref="IApiResponse{T}"/> para que o "não encontrado" da origem
/// chegue como status, e não como exceção — é resposta legítima, repassada como tal.
/// </remarks>
internal interface ISelecaoCertamePublicoApi
{
    [Get("/api/selecao/certames/{id}")]
    [Headers("Accept: application/vnd.uniplus.certame.v1+json")]
    Task<IApiResponse<CertamePublicadoPayload>> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/selecao/certames/{identificador}")]
    [Headers("Accept: application/vnd.uniplus.certame.v1+json")]
    Task<IApiResponse<CertamePublicadoPayload>> ObterPorIdentificadorAsync(
        string identificador,
        CancellationToken cancellationToken);
}
