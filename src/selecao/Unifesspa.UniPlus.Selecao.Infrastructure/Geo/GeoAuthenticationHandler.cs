namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// Carimba cada requisição ao Geo com o token M2M vigente e, se o Geo recusar o token, renova e
/// tenta de novo uma única vez. Mesmo desenho de <c>SigaaAuthenticationHandler</c>.
/// </summary>
/// <remarks>
/// Só consultas sem corpo (GET) — a conferência de endereço nunca escreve no Geo. A segunda
/// tentativa reenvia a mesma requisição, seguro porque não há corpo a reconsumir.
/// </remarks>
internal sealed class GeoAuthenticationHandler : DelegatingHandler
{
    private const string EsquemaBearer = "Bearer";

    private readonly GeoTokenProvider _tokens;

    public GeoAuthenticationHandler(GeoTokenProvider tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is not null)
        {
            throw new InvalidOperationException(
                "O cliente do Geo só envia consultas GET. Uma requisição com corpo não pode ser "
                + "reenviada após a renovação do token, porque o conteúdo já foi consumido no primeiro envio.");
        }

        string token = await _tokens.ObterAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(EsquemaBearer, token);

        HttpResponseMessage resposta = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (resposta.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return resposta;
        }

        resposta.Dispose();
        _tokens.Descartar(token);

        string renovado = await _tokens.ObterAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(EsquemaBearer, renovado);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
