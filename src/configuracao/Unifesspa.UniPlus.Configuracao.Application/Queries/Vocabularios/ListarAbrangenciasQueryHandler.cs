namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="Abrangencia"/> declararam, sem tocar em repositório.</summary>
public static class ListarAbrangenciasQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<Abrangencia>> Handle(ListarAbrangenciasQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<Abrangencia>.Valores().Select(CodigoComRotulo<Abrangencia>.De)];
    }
}
