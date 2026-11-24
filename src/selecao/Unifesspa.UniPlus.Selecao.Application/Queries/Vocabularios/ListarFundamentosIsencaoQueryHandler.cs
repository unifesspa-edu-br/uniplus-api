namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Projeta o vocabulário dos fundamentos de isenção.</summary>
public static class ListarFundamentosIsencaoQueryHandler
{
    public static IReadOnlyList<FundamentoIsencaoDto> Handle(ListarFundamentosIsencaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<FundamentoIsencao>.Todos().Select(static f =>
            new FundamentoIsencaoDto(f.Codigo, f.Rotulo, f.Descricao!))];
    }
}
