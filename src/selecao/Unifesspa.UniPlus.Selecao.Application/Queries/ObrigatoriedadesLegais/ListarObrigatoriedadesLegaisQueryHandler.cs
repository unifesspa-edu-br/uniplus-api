namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarObrigatoriedadesLegaisQuery"/>.
/// Paginação keyset por <c>Id</c> (ADR-0026 + Guid v7) com lookup batch de
/// bindings vigentes (sem N+1). Solicita <c>take + 1</c> para detectar
/// próxima página sem COUNT — mesma estratégia do <c>ListarEditaisQueryHandler</c>.
/// </summary>
public static class ListarObrigatoriedadesLegaisQueryHandler
{
    public static async Task<ListarObrigatoriedadesLegaisResult> Handle(
        ListarObrigatoriedadesLegaisQuery query,
        IObrigatoriedadeLegalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        AreaCodigo? proprietarioFiltro = null;
        if (!string.IsNullOrWhiteSpace(query.Proprietario))
        {
            Result<AreaCodigo> conv = AreaCodigo.From(query.Proprietario);
            if (conv.IsFailure)
            {
                // Filtro inválido — retorna vazio em vez de 500; lista admin
                // não vaza detalhes do shape do código de área.
                return new ListarObrigatoriedadesLegaisResult([], ProximoAfterId: null);
            }
            proprietarioFiltro = conv.Value;
        }

        IReadOnlyList<ObrigatoriedadeLegal> page = await repository.ListarPaginadoAsync(
            query.AfterId,
            query.Take + 1,
            query.TipoEditalCodigo,
            query.Categoria,
            proprietarioFiltro,
            query.Vigentes,
            cancellationToken).ConfigureAwait(false);

        if (page.Count == 0)
        {
            return new ListarObrigatoriedadesLegaisResult([], ProximoAfterId: null);
        }

        bool temProxima = page.Count > query.Take && query.Take > 0;
        IReadOnlyList<ObrigatoriedadeLegal> visiveis = temProxima
            ? [.. page.Take(query.Take)]
            : page;

        IReadOnlyDictionary<Guid, IReadOnlySet<AreaCodigo>> areasPorRegra =
            await repository.ObterAreasVigentesPorIdsAsync(
                [.. visiveis.Select(r => r.Id)],
                cancellationToken).ConfigureAwait(false);

        ObrigatoriedadeLegalDto[] items = [.. visiveis.Select(regra =>
            ObrigatoriedadeLegalMapping.ToDto(
                regra,
                areasPorRegra.TryGetValue(regra.Id, out IReadOnlySet<AreaCodigo>? areas)
                    ? areas
                    : new HashSet<AreaCodigo>()))];

        Guid? proximo = temProxima ? items[^1].Id : null;
        return new ListarObrigatoriedadesLegaisResult(items, proximo);
    }
}
