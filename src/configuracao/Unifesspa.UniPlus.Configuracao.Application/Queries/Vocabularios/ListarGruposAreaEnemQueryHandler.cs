namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Projeta os grupos do domínio, sem tocar em repositório: a lista vem de
/// <see cref="GrupoCurso.Todos"/>. Uma segunda lista aqui, ou no cliente, seria a cópia
/// que envelhece sem avisar.
/// </summary>
public static class ListarGruposAreaEnemQueryHandler
{
    public static IReadOnlyList<GrupoAreaEnemDto> Handle(ListarGruposAreaEnemQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. GrupoCurso.Todos.Select(static grupo => new GrupoAreaEnemDto(grupo.Codigo, grupo.Rotulo))];
    }
}
