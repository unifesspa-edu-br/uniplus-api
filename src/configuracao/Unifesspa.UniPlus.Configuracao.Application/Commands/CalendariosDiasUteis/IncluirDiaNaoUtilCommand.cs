namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Inclui uma data não útil num <c>CalendarioDiasUteis</c> já existente (vigente ou
/// rascunho), preservando id e versão do dataset — sem criar um novo (api#1458,
/// decisão de PO que substitui a imutabilidade original de #1016). Reaproveita
/// <see cref="DiaNaoUtilCommandItem"/>, o mesmo shape de entrada já usado em
/// <c>CriarCalendarioDiasUteisCommand</c>.
/// </summary>
public sealed record IncluirDiaNaoUtilCommand(
    Guid CalendarioId,
    DiaNaoUtilCommandItem? Item) : ICommand<Result<CalendarioDiasUteisDto>>;
