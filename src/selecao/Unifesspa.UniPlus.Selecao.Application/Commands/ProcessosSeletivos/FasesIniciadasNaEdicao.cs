namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// As fases e etapas iniciadas que a edição do cronograma e das etapas confere. Só a retificação tem versão publicada
/// contra a qual comparar: o rascunho que nunca foi publicado não tem nada iniciado. A leitura vem depois da trava do
/// processo, com o instante capturado depois dela.
/// </summary>
internal static class FasesIniciadasNaEdicao
{
    internal static Task<FasesIniciadas> ObterAsync(
        ProcessoSeletivo processo, IFasesIniciadasDoProcesso leitor, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return processo.Rascunho is null
            ? Task.FromResult(FasesIniciadas.Nenhuma)
            : leitor.ObterAsync(processo.Id, timeProvider.GetUtcNow(), cancellationToken);
    }
}
