namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterReferenciaReservaDemograficaPorIdQuery(Guid Id) : IQuery<ReferenciaReservaDemograficaDto?>;
