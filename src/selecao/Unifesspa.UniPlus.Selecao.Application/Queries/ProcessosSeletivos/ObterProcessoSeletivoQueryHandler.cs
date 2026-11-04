namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

/// <summary>
/// Lê o processo seletivo com a configuração e o projeta para o contrato REST de leitura.
/// </summary>
public static class ObterProcessoSeletivoQueryHandler
{
    public static async Task<ProcessoSeletivoDto?> Handle(
        ObterProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return processo is null ? null : ProjecaoDoProcessoSeletivo.Projetar(processo);
    }
}
