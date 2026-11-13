namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// As fases e etapas já iniciadas que a edição de um processo confere. Só a sessão de retificação aberta tem uma
/// versão publicada sobre a qual algo já começou: o rascunho que nunca foi publicado não tem fase iniciada, e a
/// leitura nem acontece. O instante vem de quem chama, lido uma vez por operação (ADR-0068).
/// </summary>
public sealed class LeituraDasFasesIniciadasNaEdicao(IFasesIniciadasDoProcesso fasesIniciadasDoProcesso)
{
    internal async Task<FasesIniciadas> DoProcessoAsync(ProcessoSeletivo processo, DateTimeOffset agora, CancellationToken cancellationToken) =>
        processo.Rascunho is null
            ? FasesIniciadas.Nenhuma
            : await fasesIniciadasDoProcesso.ObterAsync(processo.Id, agora, cancellationToken).ConfigureAwait(false);
}
