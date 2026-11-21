namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeta o vocabulário declarado no enum, sem tocar em repositório: a lista vem de
/// <see cref="CatalogoDe{TEnum}.Valores"/>, na ordem de declaração.
/// </summary>
public static class ListarEstadosFatoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EstadoFato>> Handle(ListarEstadosFatoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<EstadoFato>.Valores().Select(CodigoComRotulo<EstadoFato>.De)];
    }
}
