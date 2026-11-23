namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Leva junto os motivos escolhidos nas fases e etapas que a edição do processo deixou de ter
/// (<see cref="EscolhaDosMotivos.QueSaemComOsLugares"/>). O retrato dos lugares é tirado antes de a edição mexer no
/// processo; a remoção é rastreada e se grava com a transação que aceita a edição.
/// </summary>
public sealed class MotivosDosLugaresQueSaem(IMotivoDeDecisaoDoProcessoRepository repository)
{
    internal async Task RetirarAsync(ProcessoSeletivo processo, LugaresDaDecisao antes, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDeDecisaoDoProcesso> escolhaAtual = await repository.ListarEscolhaAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);

        foreach (MotivoDeDecisaoDoProcesso motivo in EscolhaDosMotivos.QueSaemComOsLugares(antes, processo, escolhaAtual))
        {
            repository.Remover(motivo);
        }
    }
}
