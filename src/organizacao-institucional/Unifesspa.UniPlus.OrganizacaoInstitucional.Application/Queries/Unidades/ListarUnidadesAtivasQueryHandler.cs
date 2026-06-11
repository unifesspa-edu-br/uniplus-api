namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarUnidadesAtivasQuery"/>:
/// paginação keyset-based (cursor) sobre o agregado <c>Unidade</c>, ordenando
/// pelo identificador para garantir estabilidade da janela. Solicita
/// <c>limit + 1</c> itens para detectar a próxima página sem COUNT (ADR-0026).
/// </summary>
public static class ListarUnidadesAtivasQueryHandler
{
    public static async Task<ListarUnidadesAtivasResult> Handle(
        ListarUnidadesAtivasQuery query,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<Unidade> page = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit + 1, cancellationToken)
            .ConfigureAwait(false);

        // Defesa contra Limit <= 0 (config inconsistente): Take(0) devolveria
        // array vazio e limited[^1] lançaria IndexOutOfRangeException.
        if (page.Count > query.Limit && query.Limit > 0)
        {
            UnidadeDto[] limited = [.. page.Take(query.Limit).Select(u => u.ToDto())];
            return new ListarUnidadesAtivasResult(limited, limited[^1].Id);
        }

        UnidadeDto[] items = [.. page.Select(u => u.ToDto())];
        return new ListarUnidadesAtivasResult(items, ProximoAfterId: null);
    }
}
