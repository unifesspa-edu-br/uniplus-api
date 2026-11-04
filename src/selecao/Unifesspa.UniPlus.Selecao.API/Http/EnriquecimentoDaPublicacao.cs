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
    /// <summary>
    /// Issue #1096: enriquece TODA recusa HTTP 422 da publicação com o checklist de
    /// pendências — reconsulta <see cref="ObterConformidadeProcessoSeletivoQuery"/> (mesmo
    /// <c>Domain.ProcessosSeletivos.Conformidade.ChecklistDeConformidade.AvaliarConformidade(Domain.ProcessosSeletivos.Edicao.EstadoDaConfiguracao.De(ProcessoSeletivo))</c> por trás dos seis gates estruturais,
    /// sem duplicar a regra nem manter lista paralela de códigos de erro no controller) e
    /// anexa <c>Extensions["pendencias"]</c> quando o estado atual tiver item vermelho —
    /// independentemente de qual gate produziu o 422 (CA-01/CA-02/CA-03/CA-04/CA-06). Quando
    /// o checklist estiver 100% verde, a extensão é omitida — nunca <c>pendencias: []</c>
    /// (CA-05/CA-07).
    /// </summary>
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

        ConformidadeProcessoSeletivoDto? conformidade = await queryBus
            .Send(new ObterConformidadeProcessoSeletivoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // As pendências saem com a MESMA identidade que o checklist publica — quem recebeu o
        // preview aprovado e depois este 422 compara código e dimensão, não frases. Levar só o
        // texto obrigaria o cliente a manter uma tabela de mensagens para saber onde corrigir.
        PendenciaEstruturalDto[] pendencias = conformidade?.Itens
            .Where(item => !item.Ok)
            .Select(item => new PendenciaEstruturalDto(item.Codigo, item.Dimensao, item.Mensagem))
            .ToArray() ?? [];
        if (pendencias.Length > 0)
        {
            problem.Extensions["pendencias"] = pendencias;
        }

        return actionResult;
    }

    /// <summary>
    /// Story #853: enriquece o 422 de conformidade LEGAL insuficiente com as regras
    /// reprovadas (código, descrição, base legal) — reconsulta
    /// <see cref="ObterConformidadeLegalProcessoSeletivoQuery"/> para montar
    /// <c>Extensions["obrigatoriedadesReprovadas"]</c>.
    /// </summary>
    internal static async Task<IActionResult> EnriquecerComObrigatoriedadesReprovadasAsync(
        IQueryBus queryBus,
        Result resultado,
        IActionResult actionResult,
        Guid id,
        DateTimeOffset? periodoInscricaoInformado,
        CancellationToken cancellationToken)
    {
        if (!resultado.HasErrorCode("ProcessoSeletivo.ConformidadeLegalInsuficiente")
            || actionResult is not ObjectResult { Value: ProblemDetails problem })
        {
            return actionResult;
        }

        // A recusa que está sendo enriquecida é a que vale: se a reconsulta não chega a uma data de
        // referência, o 422 sai sem a lista, nunca trocado pela falha da consulta auxiliar.
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

        return actionResult;
    }
}
