namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Leva junto os motivos escolhidos nas fases e etapas que a edição do processo deixou de ter: os pendentes saem com o
/// lugar, e o motivo já publicado recusa a edição inteira (<see cref="EscolhaDosMotivos.QueSaemComOsLugares"/>). O
/// retrato dos lugares é tirado antes de a edição mexer no processo; a remoção é rastreada e se grava com a transação
/// que aceita a edição.
/// </summary>
public sealed class MotivosDosLugaresQueSaem(IMotivoDoProcessoRepository repository)
{
    internal async Task<Result> RetirarAsync(ProcessoSeletivo processo, LugaresDaDecisao antes, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhaAtual = await repository.ListarEscolhaAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyList<MotivoDoProcesso>> queSaem = EscolhaDosMotivos.QueSaemComOsLugares(antes, processo, escolhaAtual);
        if (queSaem.IsFailure)
        {
            return Result.ValidationFailure(queSaem.Errors);
        }

        foreach (MotivoDoProcesso motivo in queSaem.Value!)
        {
            repository.Remover(motivo);
        }

        return Result.Success();
    }
}
