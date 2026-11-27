namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Obtém um motivo pelo Id. Recusa com o erro de motivo não encontrado quando inexistente.</summary>
public sealed record ObterMotivoDeDecisaoQuery(Guid Id) : IQuery<Result<MotivoDeDecisaoDto>>;
