namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// A listagem carrega apenas a raiz (sem a configuração) e projeta em
/// <see cref="ProcessoSeletivoResumoDto"/> — omitir as coleções de configuração
/// evita que um processo já configurado pareça incompleto na lista. O detalhe
/// completo vive em <c>GET /{id}</c> (<see cref="ObterProcessoSeletivoQuery"/>).
/// </summary>
public static class ListarProcessosSeletivosQueryHandler
{
    public static async Task<ListarProcessosSeletivosResult> Handle(
        ListarProcessosSeletivosQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        (IReadOnlyList<ProcessoSeletivo> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await processoSeletivoRepository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        ProcessoSeletivoResumoDto[] items = [.. itens.Select(Project)];
        return new ListarProcessosSeletivosResult(items, anteriorAfterId, proximoAfterId);
    }

    private static ProcessoSeletivoResumoDto Project(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        processo.Tipo.ToString(),
        processo.Status.ToString(),
        processo.CreatedAt);
}
