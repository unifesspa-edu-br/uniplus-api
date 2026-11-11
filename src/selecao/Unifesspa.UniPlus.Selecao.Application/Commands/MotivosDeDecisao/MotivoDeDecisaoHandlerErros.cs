namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Erros que os handlers do cadastro e da escolha produzem por si — os que decorrem do estado do
/// repositório, e não de invariante do agregado.
/// </summary>
internal static class MotivoDeDecisaoHandlerErros
{
    public static DomainError NaoEncontrado(Guid id) =>
        new(MotivoDeDecisaoErrorCodes.NaoEncontrado, $"Motivo de decisão {id} não encontrado.");

    public static DomainError CodigoJaExiste() =>
        new(MotivoDeDecisaoErrorCodes.CodigoJaExiste, "Já existe um motivo de decisão com o código informado neste tipo.");

    public static DomainError SituacaoAlteradaConcorrentemente() =>
        new(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente,
            "A situação do motivo de decisão foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}
