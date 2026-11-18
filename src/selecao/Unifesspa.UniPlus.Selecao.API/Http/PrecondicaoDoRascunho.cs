namespace Unifesspa.UniPlus.Selecao.API.Http;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O <c>If-Match</c> das mutações do rascunho, conferido antes de ler o corpo: header malformado é 400 e header ausente é 428,
/// nessa ordem, ambos antes do limite do corpo. Quem decide se a revisão casa é o handler, sob a trava da linha.
/// </summary>
internal static class PrecondicaoDoRascunho
{
    public static Result<PrecondicaoIfMatch> Exigir(string? ifMatch)
    {
        Result<PrecondicaoIfMatch> precondicao = IfMatchHeader.Analisar(ifMatch);
        if (precondicao.IsFailure)
        {
            return precondicao;
        }

        Result exigida = RascunhoDeProcessoSeletivo.ExigirPrecondicao(precondicao.Value!);
        return exigida.IsFailure
            ? Result<PrecondicaoIfMatch>.ValidationFailure(exigida.Errors)
            : precondicao;
    }
}
