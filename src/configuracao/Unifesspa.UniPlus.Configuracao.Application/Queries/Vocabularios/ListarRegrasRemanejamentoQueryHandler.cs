namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="RegraRemanejamento"/> declararam, sem tocar em repositório.</summary>
public static class ListarRegrasRemanejamentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<RegraRemanejamento>> Handle(ListarRegrasRemanejamentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<RegraRemanejamento>.Valores().Select(CodigoComRotulo<RegraRemanejamento>.De)];
    }
}
