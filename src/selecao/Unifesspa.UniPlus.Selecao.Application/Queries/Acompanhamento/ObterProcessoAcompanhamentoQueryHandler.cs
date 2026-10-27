namespace Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

public static class ObterProcessoAcompanhamentoQueryHandler
{
    public static async Task<ProcessoAcompanhamentoResumoDto?> Handle(
        ObterProcessoAcompanhamentoQuery query,
        IProcessoSeletivoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        ProcessoSeletivo? processo = await repository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return processo is null ? null : new ProcessoAcompanhamentoResumoDto(processo.Id, processo.Nome, processo.Status);
    }
}
