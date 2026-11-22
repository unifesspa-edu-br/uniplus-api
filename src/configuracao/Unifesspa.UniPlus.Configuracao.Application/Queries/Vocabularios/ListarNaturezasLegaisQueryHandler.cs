namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="NaturezaLegal"/> declararam, sem tocar em repositório.</summary>
public static class ListarNaturezasLegaisQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<NaturezaLegal>> Handle(ListarNaturezasLegaisQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<NaturezaLegal>.Valores().Select(CodigoComRotulo<NaturezaLegal>.De)];
    }
}
