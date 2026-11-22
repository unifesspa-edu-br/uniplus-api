namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lista os critérios de votação na ordem de declaração.</summary>
public static class ListarCriteriosDeVotacaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<CriterioDeVotacao>> Handle(ListarCriteriosDeVotacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<CriterioDeVotacao>.Valores().Select(CodigoComRotulo<CriterioDeVotacao>.De)];
    }
}
