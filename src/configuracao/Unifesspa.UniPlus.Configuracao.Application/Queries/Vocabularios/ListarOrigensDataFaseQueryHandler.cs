namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="OrigemDataFase"/> declararam, sem tocar em repositório.</summary>
public static class ListarOrigensDataFaseQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<OrigemDataFase>> Handle(ListarOrigensDataFaseQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<OrigemDataFase>.Valores().Select(CodigoComRotulo<OrigemDataFase>.De)];
    }
}
