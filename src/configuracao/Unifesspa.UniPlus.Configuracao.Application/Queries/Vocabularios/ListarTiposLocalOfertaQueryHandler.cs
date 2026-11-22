namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="TipoLocalOferta"/> declararam, sem tocar em repositório.</summary>
public static class ListarTiposLocalOfertaQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TipoLocalOferta>> Handle(ListarTiposLocalOfertaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<TipoLocalOferta>.Valores().Select(CodigoComRotulo<TipoLocalOferta>.De)];
    }
}
