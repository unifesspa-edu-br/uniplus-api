namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Projeta as áreas do domínio, sem tocar em repositório: a lista vem de
/// <see cref="PesoAreaEnem.Areas"/>. Uma segunda lista aqui, ou no cliente, seria a cópia
/// que envelhece sem avisar.
/// </summary>
public static class ListarAreasPesoAreaEnemQueryHandler
{
    public static IReadOnlyList<AreaPesoAreaEnemDto> Handle(ListarAreasPesoAreaEnemQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. PesoAreaEnem.Areas.Select(static area => new AreaPesoAreaEnemDto(area.Codigo, area.Rotulo))];
    }
}
