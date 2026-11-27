namespace Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

using Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Aplica o limite de tamanho ao corpo da requisição antes da autenticação, para que a recusa valha para quem chama sem
/// login e para quem chama sem permissão, e antes de qualquer leitura ou validação do conteúdo.
/// </summary>
/// <remarks>
/// O limite vai ao servidor, que interrompe a leitura de um corpo sem tamanho declarado ao passar dele. O tamanho declarado
/// no cabeçalho é conferido aqui, sem ler o corpo. A leitura interrompida chega ao <c>GlobalExceptionMiddleware</c>, que a
/// traduz na mesma resposta.
/// </remarks>
public sealed partial class LimiteDoCorpoDaRequisicaoMiddleware(
    RequestDelegate next,
    IOptions<LimiteDoCorpoDaRequisicaoOptions> opcoes,
    IProblemTypeUriFactory problemTypeUriFactory,
    ILogger<LimiteDoCorpoDaRequisicaoMiddleware> logger)
{
    private readonly LimiteDoCorpoDaRequisicaoOptions _opcoes = opcoes.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long limite = LimiteDoCorpoDaOperacao.Declarado(context, _opcoes);
        AplicarNoServidor(context, limite);

        if (context.Request.ContentLength > limite)
        {
            LogCorpoAcimaDoLimite(logger, limite, context.Request.Path);
            await RespostaDoCorpoAcimaDoLimite.EscreverAsync(context, limite, problemTypeUriFactory).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    // O servidor recusa a escrita quando a leitura do corpo já começou; o limite não pode mais mudar e o tamanho declarado
    // já foi conferido.
    private static void AplicarNoServidor(HttpContext context, long limite)
    {
        IHttpMaxRequestBodySizeFeature? servidor = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (servidor is { IsReadOnly: false })
        {
            servidor.MaxRequestBodySize = limite;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Corpo da requisição acima do limite de {Limite} bytes em {Path}")]
    private static partial void LogCorpoAcimaDoLimite(ILogger logger, long limite, PathString path);
}
