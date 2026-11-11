namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Interfaces;

/// <summary>
/// O que o descarte da sessão de retificação apaga além de repor a configuração: o rascunho do ato
/// que não será publicado e as inclusões de motivo que não chegaram a congelar.
/// </summary>
internal static class DescarteDaSessao
{
    internal static async Task ApagarPendenciasAsync(
        IRascunhoDePublicacaoRepository rascunhos, IProcessoSeletivoRepository processos, Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        await rascunhos.ApagarDoProcessoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        await processos.DescartarMotivosPendentesAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
    }
}
