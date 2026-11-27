namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterReferenciaReservaDemograficaPorIdQuery(Guid Id) : IQuery<Result<ReferenciaReservaDemograficaDto>>;
