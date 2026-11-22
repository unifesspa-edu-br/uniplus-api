namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lista os desempates da votação na ordem de declaração.</summary>
public static class ListarDesempatesDaVotacaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<DesempateDaVotacao>> Handle(ListarDesempatesDaVotacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<DesempateDaVotacao>.Valores().Select(CodigoComRotulo<DesempateDaVotacao>.De)];
    }
}
