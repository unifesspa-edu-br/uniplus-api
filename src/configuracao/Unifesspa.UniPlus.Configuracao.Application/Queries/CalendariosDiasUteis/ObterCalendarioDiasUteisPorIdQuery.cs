namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterCalendarioDiasUteisPorIdQuery(Guid Id) : IQuery<CalendarioDiasUteisDto?>;
