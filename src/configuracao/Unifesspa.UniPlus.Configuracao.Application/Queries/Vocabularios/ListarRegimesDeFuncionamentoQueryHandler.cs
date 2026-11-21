namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="RegimeDeFuncionamento"/> declararam, sem tocar em repositório.</summary>
public static class ListarRegimesDeFuncionamentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<RegimeDeFuncionamento>> Handle(ListarRegimesDeFuncionamentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<RegimeDeFuncionamento>.Valores().Select(CodigoComRotulo<RegimeDeFuncionamento>.De)];
    }
}
