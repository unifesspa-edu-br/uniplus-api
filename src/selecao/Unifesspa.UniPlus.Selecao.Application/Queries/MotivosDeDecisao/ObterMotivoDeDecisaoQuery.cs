namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Obtém um motivo pelo Id. Devolve nulo quando inexistente.</summary>
public sealed record ObterMotivoDeDecisaoQuery(Guid Id) : IQuery<MotivoDeDecisaoDto?>;
