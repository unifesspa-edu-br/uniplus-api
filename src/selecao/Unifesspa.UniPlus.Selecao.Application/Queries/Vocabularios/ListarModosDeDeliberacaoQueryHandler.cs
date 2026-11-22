namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lista os modos de deliberação na ordem de declaração.</summary>
public static class ListarModosDeDeliberacaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ModoDeDeliberacao>> Handle(ListarModosDeDeliberacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<ModoDeDeliberacao>.Valores().Select(CodigoComRotulo<ModoDeDeliberacao>.De)];
    }
}
