namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisaoDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Inclui num lugar do processo, uma fase ou uma etapa, um motivo do cadastro (UNI-REQ-0285), só em rascunho.
/// </summary>
public sealed record IncluirMotivoDeDecisaoNoProcessoCommand(
    Guid ProcessoSeletivoId,
    LugarDaDecisao Lugar,
    Guid MotivoDeDecisaoId) : ICommand<Result>;
