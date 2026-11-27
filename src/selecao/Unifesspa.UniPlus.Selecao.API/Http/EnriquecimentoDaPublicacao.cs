namespace Unifesspa.UniPlus.Selecao.API.Http;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Contracts.Responses;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// Acrescenta ao problema 422 de uma publicação ou retificação recusada o que o operador
/// precisa para corrigir: as pendências do checklist estrutural e as obrigatoriedades legais
/// reprovadas.
/// </summary>
internal static class EnriquecimentoDaPublicacao
{
    // A extensão sai só quando o checklist tem item vermelho, nunca como lista vazia, e
    // independe de qual regra produziu o 422: a regra vem do checklist, não de uma lista de códigos.
    internal static async Task<IActionResult> EnriquecerComPendenciasEstruturaisAsync(
        IQueryBus queryBus, IActionResult actionResult, Guid id, CancellationToken cancellationToken)
    {
        if (actionResult is not ObjectResult
            {
                StatusCode: StatusCodes.Status422UnprocessableEntity, Value: ProblemDetails problem,
            })
        {
            return actionResult;
        }

        Result<ConformidadeProcessoSeletivoDto> conformidade = await queryBus
            .Send(new ObterConformidadeProcessoSeletivoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // As pendências saem com a MESMA identidade que o checklist publica — quem recebeu o
        // preview aprovado e depois este 422 compara código e dimensão, não frases. Levar só o
        // texto obrigaria o cliente a manter uma tabela de mensagens para saber onde corrigir.
        PendenciaEstruturalDto[] pendencias = conformidade.Value?.Itens
            .Where(item => !item.Ok)
            .Select(item => new PendenciaEstruturalDto(item.Codigo, item.Dimensao, item.Mensagem))
            .ToArray() ?? [];
        if (pendencias.Length > 0)
        {
            problem.Extensions["pendencias"] = pendencias;
        }

        return actionResult;
    }

    internal static async Task<IActionResult> EnriquecerComObrigatoriedadesReprovadasAsync(
        IQueryBus queryBus,
        Result resultado,
        IActionResult actionResult,
        Guid id,
        DateTimeOffset? periodoInscricaoInformado,
        CancellationToken cancellationToken)
    {
        if (ConformidadeLegalRecusada(resultado) && actionResult is ObjectResult { Value: ProblemDetails problem })
        {
            await AcrescentarReprovadasAsync(queryBus, problem, id, periodoInscricaoInformado, cancellationToken).ConfigureAwait(false);
        }

        return actionResult;
    }

    // A conformidade legal é uma causa entre as que a recusa acumula, e nem sempre a primeira.
    private static bool ConformidadeLegalRecusada(Result resultado) =>
        resultado.Errors.Any(static erro => erro.Error.Code == "ProcessoSeletivo.ConformidadeLegalInsuficiente");

    // A recusa que está sendo enriquecida é a que vale: se a reconsulta não chega a uma data de referência, o 422 sai sem a
    // lista, nunca trocado pela falha da consulta auxiliar.
    private static async Task AcrescentarReprovadasAsync(
        IQueryBus queryBus, ProblemDetails problem, Guid id, DateTimeOffset? periodoInscricaoInformado, CancellationToken cancellationToken)
    {
        Result<ConformidadeLegalProcessoSeletivoDto> conformidade = await queryBus
            .Send(new ObterConformidadeLegalProcessoSeletivoQuery(id, PeriodoInscricaoInformado: periodoInscricaoInformado), cancellationToken)
            .ConfigureAwait(false);
        if (conformidade.IsSuccess)
        {
            problem.Extensions["obrigatoriedadesReprovadas"] = conformidade.Value!.Regras
                .Where(regra => !regra.Aprovada)
                .Select(regra => new { regra.RegraCodigo, regra.DescricaoHumana, regra.BaseLegal, regra.Motivo })
                .ToArray();
        }
    }
}
