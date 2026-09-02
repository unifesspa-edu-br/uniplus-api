namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Carimba cada requisição ao SIGAA com o token de acesso vigente e, se a origem
/// recusar o token, renova e tenta de novo uma única vez.
/// </summary>
/// <remarks>
/// <para>
/// Fica na parte interna da cadeia de envio, dentro da política de retentativa. Assim
/// cada tentativa relê o token guardado em vez de repetir um cabeçalho carimbado uma vez
/// só, e a recusa por token vencido é resolvida aqui — onde há como renovar — em vez de
/// subir para a política, que a classificaria como falha não repetível e desistiria.
/// </para>
/// <para>
/// A segunda tentativa reenvia a mesma requisição. Isso é seguro porque as chamadas ao
/// SIGAA são consultas sem corpo: um corpo já teria sido consumido no primeiro envio e
/// chegaria vazio no segundo. O construtor recusa qualquer requisição com corpo para que
/// essa premissa não se perca silenciosamente se o cliente ganhar uma escrita no futuro.
/// </para>
/// </remarks>
internal sealed class SigaaAuthenticationHandler : DelegatingHandler
{
    private const string EsquemaBearer = "Bearer";

    private readonly SigaaTokenProvider _tokens;

    public SigaaAuthenticationHandler(SigaaTokenProvider tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is not null)
        {
            throw new InvalidOperationException(
                "Este cliente só envia consultas sem corpo ao SIGAA. Uma requisição com corpo "
                + "não pode ser reenviada após a renovação do token, porque o conteúdo já foi "
                + "consumido no primeiro envio.");
        }

        string token = await _tokens.ObterAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(EsquemaBearer, token);

        HttpResponseMessage resposta = await base
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (resposta.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return resposta;
        }

        // A recusa é descartada explicitamente: sem isso a conexão fica presa até a
        // coleta de lixo, e uma sincronização inteira de páginas recusadas esgotaria o
        // conjunto de conexões.
        resposta.Dispose();

        _tokens.Descartar(token);

        string renovado = await _tokens.ObterAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(EsquemaBearer, renovado);

        // Uma única repetição. Se a origem recusar de novo, o problema não é o token
        // vencido — é a credencial ou a permissão, e insistir vira laço.
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
