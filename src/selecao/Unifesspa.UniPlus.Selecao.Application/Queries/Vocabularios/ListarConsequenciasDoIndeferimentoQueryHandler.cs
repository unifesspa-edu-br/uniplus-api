namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece o vocabulário das consequências do indeferimento de um documento exigido.</summary>
public static class ListarConsequenciasDoIndeferimentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ConsequenciaDoIndeferimento>> Handle(ListarConsequenciasDoIndeferimentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<ConsequenciaDoIndeferimento>.Valores().Select(CodigoComRotulo<ConsequenciaDoIndeferimento>.De)];
    }
}
