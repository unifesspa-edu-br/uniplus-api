namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterPrecedenciaFasePorIdQuery(Guid Id) : IQuery<PrecedenciaFaseDto?>;
