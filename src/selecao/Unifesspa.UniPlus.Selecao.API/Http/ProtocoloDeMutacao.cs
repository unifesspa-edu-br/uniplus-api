namespace Unifesspa.UniPlus.Selecao.API.Http;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Protocolo de concorrência das rotas que alteram a configuração do processo: o
/// <c>If-Match</c> entra na requisição e o <c>ETag</c> novo sai na resposta.
/// </summary>
internal static class ProtocoloDeMutacao
{
    /// <summary>
    /// Decodifica o <c>If-Match</c> — <b>só a sintaxe</b> (ADR-0110 D5). Se ele é
    /// <b>obrigatório</b>, e se ele <b>casa</b>, quem decide é o handler, sob o lock: a
    /// obrigatoriedade depende de haver sessão editorial aberta, e o transporte não carrega
    /// o agregado.
    /// </summary>
    internal static bool TentarLerPrecondicao(
        IDomainErrorMapper mapper,
        string? ifMatch,
        out PrecondicaoIfMatch precondicao,
        out IActionResult? malformada)
    {
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            precondicao = PrecondicaoIfMatch.Ausente;
            malformada = analise.ToActionResult(mapper);
            return false;
        }

        precondicao = analise.Value!;
        malformada = null;
        return true;
    }

    /// <summary>
    /// 204 com o <c>ETag</c> <b>novo</b> quando a mutação correu sob sessão editorial; 204
    /// nu quando o processo está em rascunho (não há sessão, não há tag).
    /// </summary>
    /// <remarks>
    /// Devolver o tag novo é o que permite ao cliente encadear a próxima edição sem um
    /// <c>GET</c> no meio — a revisão acabou de ser incrementada, e o tag que ele tinha em
    /// mãos já não vale.
    /// </remarks>
    internal static IActionResult ResponderMutacao(
        IDomainErrorMapper mapper, HttpResponse response, Result<MutacaoAceita> resultado)
    {
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(mapper);
        }

        EmitirETag(response, resultado.Value!.ETag);
        return new NoContentResult();
    }

    /// <summary>
    /// Devolve o <c>ETag</c> da revisão nova; sem sessão editorial não há tag e o header fica ausente.
    /// </summary>
    internal static void EmitirETag(HttpResponse response, string? etag)
    {
        if (etag is not null)
        {
            response.Headers.ETag = etag;
        }
    }
}
