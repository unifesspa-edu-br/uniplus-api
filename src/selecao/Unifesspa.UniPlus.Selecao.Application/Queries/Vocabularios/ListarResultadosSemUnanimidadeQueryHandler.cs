namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lista os resultados sem unanimidade na ordem de declaração.</summary>
public static class ListarResultadosSemUnanimidadeQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ResultadoSemUnanimidade>> Handle(ListarResultadosSemUnanimidadeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<ResultadoSemUnanimidade>.Valores().Select(CodigoComRotulo<ResultadoSemUnanimidade>.De)];
    }
}
