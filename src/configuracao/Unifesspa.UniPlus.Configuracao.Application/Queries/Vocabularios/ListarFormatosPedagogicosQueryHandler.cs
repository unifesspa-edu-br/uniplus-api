namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="FormatoPedagogico"/> declararam, sem tocar em repositório.</summary>
public static class ListarFormatosPedagogicosQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FormatoPedagogico>> Handle(ListarFormatosPedagogicosQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<FormatoPedagogico>.Valores().Select(CodigoComRotulo<FormatoPedagogico>.De)];
    }
}
