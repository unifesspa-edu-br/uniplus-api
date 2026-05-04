namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarEditaisQuery"/>: paginação
/// keyset-based (cursor) sobre o agregado <c>Edital</c>, ordenando pelo
/// identificador para garantir estabilidade da janela. Solicita
/// <c>limit + 1</c> itens para detectar a existência de próxima página sem
/// precisar de COUNT, conforme ADR-0026.
/// </summary>
public static class ListarEditaisQueryHandler
{
    public static async Task<ListarEditaisResult> Handle(
        ListarEditaisQuery query,
        IEditalRepository editalRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(editalRepository);

        IReadOnlyList<Edital> page = await editalRepository
            .ListarPaginadoAsync(query.AfterId, query.Limit + 1, cancellationToken)
            .ConfigureAwait(false);

        if (page.Count > query.Limit)
        {
            EditalDto[] limited = [.. page.Take(query.Limit).Select(Project)];
            Guid proximo = limited[^1].Id;
            return new ListarEditaisResult(limited, proximo);
        }

        EditalDto[] items = [.. page.Select(Project)];
        return new ListarEditaisResult(items, ProximoAfterId: null);
    }

    private static EditalDto Project(Edital edital) => new(
        edital.Id,
        edital.NumeroEdital.ToString(),
        edital.Titulo,
        edital.TipoProcesso.ToString(),
        edital.Status.ToString(),
        edital.MaximoOpcoesCurso,
        edital.BonusRegionalHabilitado,
        edital.CreatedAt);
}
