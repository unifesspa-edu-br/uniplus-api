namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// A recusa que o handler devolve depois de gravar estado. O filtro de idempotência a reconhece
/// pelo tipo e armazena a resposta, em vez de liberar a chave como faz com a recusa que não
/// executou nada.
/// </summary>
internal sealed class RecusaAposGravacaoResult : ObjectResult
{
    public RecusaAposGravacaoResult(ProblemDetails problem)
        : base(problem)
    {
    }
}
