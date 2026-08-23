namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Erros que os handlers do catálogo produzem por si — os que decorrem do
/// estado do repositório, e não de invariante do agregado.
/// </summary>
internal static class MotivoDecisaoIsencaoHandlerErros
{
    public static DomainError NaoEncontrado(Guid id) =>
        new(MotivoDecisaoIsencaoErrorCodes.NaoEncontrado,
            $"Motivo de decisão de isenção {id} não encontrado.");
}
