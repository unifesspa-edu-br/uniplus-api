namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="TurnoOferta"/> declararam, sem tocar em repositório.</summary>
public static class ListarTurnosOfertaQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TurnoOferta>> Handle(ListarTurnosOfertaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<TurnoOferta>.Valores().Select(CodigoComRotulo<TurnoOferta>.De)];
    }
}
