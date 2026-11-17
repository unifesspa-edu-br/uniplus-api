namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;

using Kernel.Results;

/// <summary>
/// Os motivos escolhidos, que a publicação, a retificação de uma vez e o fechamento da sessão conferem por último. Os
/// motivos ainda não congelados recopiam a descrição vigente e recusam o que veio de motivo desativado
/// (UNI-REQ-0122); a cobertura que as decisões do processo exigem é conferida
/// (<see cref="CoberturaDosMotivos"/>, UNI-REQ-0123); só então a escolha congela.
/// </summary>
/// <remarks>
/// O congelamento acontece antes de a transição do agregado aceitar a versão; se algo depois dele
/// recusar, o descarte do rastreamento que envolve as três operações desfaz também este.
/// </remarks>
public sealed class ConferenciaDosMotivosAoCongelar(IProcessoSeletivoRepository repository)
{
    internal async Task<Result> ConferirAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos = await repository.ListarMotivosEscolhidosAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        MotivoDoProcesso[] motivos = [.. escolhidos.Select(static e => e.Escolhido)];

        Result conferencia = MotivosAoCongelar.Recopiar(escolhidos);
        conferencia = conferencia.IsSuccess ? CoberturaDosMotivos.Conferir(processo, motivos) : conferencia;
        if (conferencia.IsSuccess)
        {
            MotivosAoCongelar.Congelar(motivos);
        }

        return conferencia;
    }
}
