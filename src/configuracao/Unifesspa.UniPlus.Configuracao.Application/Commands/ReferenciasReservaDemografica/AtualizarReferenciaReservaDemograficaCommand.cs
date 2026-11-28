namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// <c>CensoReferencia</c> e <c>BaseLegal</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem valor default, para o schema OpenAPI continuar listando-os
/// como obrigatórios; nulos, para o campo ausente escapar do <c>[ApiController]</c>
/// e chegar à validação de domínio. Os três percentuais são <c>decimal?</c> pelo mesmo motivo: o zero é um
/// valor válido, então a ausência não pode virar zero em silêncio; ausente ou nulo é recusado com 422.
/// </remarks>
public sealed record AtualizarReferenciaReservaDemograficaCommand(
    Guid Id,
    string? CensoReferencia,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422). Zero é um valor válido.")]
    decimal? PpiPercentual,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422). Zero é um valor válido.")]
    decimal? QuilombolaPercentual,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422). Zero é um valor válido.")]
    decimal? PcdPercentual,
    string? BaseLegal) : ICommand<Result>;
