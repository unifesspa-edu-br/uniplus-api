namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Projeta o vocabulário declarado no enum, sem tocar em repositório: a lista vem de
/// <see cref="CatalogoDe{TEnum}.Valores"/>, na ordem de declaração.
/// </summary>
public static class ListarNaturezasConceitoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<NaturezaConceito>> Handle(ListarNaturezasConceitoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<NaturezaConceito>.Valores().Select(CodigoComRotulo<NaturezaConceito>.De)];
    }
}
