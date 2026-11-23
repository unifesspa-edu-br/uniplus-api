namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisaoDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Retira de um lugar do processo um motivo escolhido, só em rascunho.</summary>
public sealed record RetirarMotivoDeDecisaoDoProcessoCommand(
    Guid ProcessoSeletivoId,
    LugarDaDecisao Lugar,
    Guid MotivoDeDecisaoId) : ICommand<Result>;
