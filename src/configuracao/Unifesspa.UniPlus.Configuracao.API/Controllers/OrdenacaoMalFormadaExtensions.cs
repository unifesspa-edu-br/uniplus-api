namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Traduz a recusa de forma da expressão de ordenação para a resposta HTTP.
/// </summary>
/// <remarks>
/// Fica separado do catálogo de erros de domínio de propósito: o que se recusa
/// aqui é a <b>forma</b> do parâmetro — vírgulas seguidas, campo repetido —, coisa
/// que nunca chega à Application. Saber se o campo existe já é outra conversa, e
/// essa acontece no handler, contra o catálogo do recurso.
/// </remarks>
internal static class OrdenacaoMalFormadaExtensions
{
    internal static IActionResult ParaResposta(this SortExpressionError erro) =>
        new UnprocessableEntityObjectResult(new ProblemDetails
        {
            Title = "Ordenação inválida",
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = erro switch
            {
                SortExpressionError.CampoVazio =>
                    "O parâmetro 'sort' tem um campo vazio. Informe os campos separados por vírgula, "
                    + "cada um opcionalmente prefixado por '-' para ordem decrescente.",
                SortExpressionError.CampoRepetido =>
                    "O parâmetro 'sort' repete um campo. Cada campo pode aparecer uma vez só.",
                _ => "O parâmetro 'sort' é inválido.",
            },
            Type = "uniplus.configuracao.consulta.ordenacao_mal_formada",
        });
}
