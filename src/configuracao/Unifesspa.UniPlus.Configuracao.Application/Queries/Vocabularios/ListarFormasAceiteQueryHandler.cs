namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="FormaAceite"/> declararam, sem tocar em repositório.</summary>
public static class ListarFormasAceiteQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FormaAceite>> Handle(ListarFormasAceiteQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<FormaAceite>.Valores().Select(CodigoComRotulo<FormaAceite>.De)];
    }
}
