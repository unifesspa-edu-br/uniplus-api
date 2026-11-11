namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;

using Kernel.Results;

/// <summary>
/// O último portão da publicação, da retificação de uma vez e do fechamento da sessão, depois de
/// todos os outros: refaz a cópia dos motivos ainda não congelados, recusando o que veio de motivo
/// desativado (UNI-REQ-0122), confere a cobertura que as decisões do processo exigem
/// (<see cref="CoberturaDosMotivos"/>, UNI-REQ-0123) e só então congela a escolha.
/// </summary>
/// <remarks>
/// O congelamento acontece antes de a transição do agregado aceitar a versão; se algo depois dele
/// recusar, o descarte do rastreamento que envolve as três operações desfaz também este.
/// </remarks>
internal static class ConferenciaDosMotivosAoCongelar
{
    internal static async Task<Result> ConferirAsync(
        IMotivoDoProcessoRepository repository, ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos = await repository.ListarComOrigemAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        MotivoDoProcesso[] motivos = [.. escolhidos.Select(static e => e.Escolhido)];

        Result conferencia = MotivosAoCongelar.Recopiar(escolhidos);
        conferencia = conferencia.IsSuccess ? CoberturaDosMotivos.Conferir(processo, motivos) : conferencia;
        if (conferencia.IsSuccess)
        {
            MotivosAoCongelar.Congelar(motivos);
        }

        return conferencia;
    }

    /// <summary>Retira as escolhas ainda não congeladas — as que a sessão descartada incluiu.</summary>
    internal static async Task DescartarPendentesAsync(
        IMotivoDoProcessoRepository repository, Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<MotivoDoProcesso> escolhidos = await repository.ListarDoProcessoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        foreach (MotivoDoProcesso pendente in escolhidos.Where(static e => !e.Congelado))
        {
            repository.Remover(pendente);
        }
    }
}
