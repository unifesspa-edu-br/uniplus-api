namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz a recusa de forma da expressão de ordenação para a resposta HTTP.
/// </summary>
/// <remarks>
/// <para>
/// O que se recusa aqui é a <b>forma</b> do parâmetro — vírgulas seguidas, campo repetido —, coisa
/// que nunca chega à Application. Saber se o campo existe já é outra conversa, e essa acontece no
/// handler, contra o catálogo do recurso.
/// </para>
/// <para>
/// A resposta sai pelo mesmo caminho de todo erro de domínio da API: a recusa vira um
/// <see cref="DomainError"/> com o código compartilhado das listagens, e o mapeador resolve
/// status, <c>type</c> (URI) e <c>code</c>. Montar o <c>ProblemDetails</c> à mão produzia o único
/// 422 do sistema sem as extensões <c>code</c> e <c>traceId</c>, e com um código de taxonomia
/// ocupando o lugar do <c>type</c>, que é URI.
/// </para>
/// <para>
/// Vive no compartilhado porque a recusa não tem domínio: as listagens de qualquer módulo leem o
/// mesmo parâmetro pelo mesmo parser.
/// </para>
/// </remarks>
public static class OrdenacaoMalFormadaExtensions
{
    /// <summary>Resposta HTTP da recusa de forma do parâmetro <c>sort</c>.</summary>
    public static IActionResult ParaResposta(this SortExpressionError erro, IDomainErrorMapper mapper) =>
        Result.Failure(new DomainError(ConsultaErrorCodes.OrdenacaoMalFormada, Detalhe(erro)))
            .ToActionResult(mapper);

    private static string Detalhe(SortExpressionError erro) => erro switch
    {
        SortExpressionError.CampoVazio =>
            "O parâmetro 'sort' tem um campo vazio. Informe os campos separados por vírgula, "
            + "cada um opcionalmente prefixado por '-' para ordem decrescente.",
        SortExpressionError.CampoRepetido =>
            "O parâmetro 'sort' repete um campo. Cada campo pode aparecer uma vez só.",
        _ => "O parâmetro 'sort' é inválido.",
    };
}
