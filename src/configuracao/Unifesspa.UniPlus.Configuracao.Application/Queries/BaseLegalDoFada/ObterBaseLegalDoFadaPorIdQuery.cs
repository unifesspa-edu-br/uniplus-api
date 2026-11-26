namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterBaseLegalDoFadaPorIdQuery(Guid Id) : IQuery<BaseLegalDoFadaDto?>;
