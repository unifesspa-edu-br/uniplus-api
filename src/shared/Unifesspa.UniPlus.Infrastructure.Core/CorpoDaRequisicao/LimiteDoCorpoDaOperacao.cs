namespace Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

/// <summary>Quanto de corpo a operação da requisição aceita.</summary>
internal static class LimiteDoCorpoDaOperacao
{
    /// <summary>O do <c>[RequestSizeLimit]</c> da operação, quando declarado; senão o geral das opções.</summary>
    internal static long Declarado(HttpContext context, LimiteDoCorpoDaRequisicaoOptions opcoes) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
        ?? opcoes.TamanhoMaximoEmBytes;

    /// <summary>O que o servidor aplicou à requisição; sem o servidor informar, o padrão das opções.</summary>
    internal static long EmVigor(HttpContext context) =>
        context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize
        ?? LimiteDoCorpoDaRequisicaoOptions.PadraoEmBytes;
}
