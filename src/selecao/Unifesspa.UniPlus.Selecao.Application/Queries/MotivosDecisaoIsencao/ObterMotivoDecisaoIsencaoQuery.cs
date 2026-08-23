namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Obtém um motivo pelo Id. Devolve nulo quando inexistente.</summary>
public sealed record ObterMotivoDecisaoIsencaoQuery(Guid Id) : IQuery<MotivoDecisaoIsencaoDto?>;
