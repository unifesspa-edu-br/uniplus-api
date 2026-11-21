namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="ProgramaDeOferta"/> declararam, sem tocar em repositório.</summary>
public static class ListarProgramasDeOfertaQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ProgramaDeOferta>> Handle(ListarProgramasDeOfertaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<ProgramaDeOferta>.Valores().Select(CodigoComRotulo<ProgramaDeOferta>.De)];
    }
}
