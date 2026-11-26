namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece o vocabulário das consequências do indeferimento de um documento exigido.</summary>
public static class ListarConsequenciasDoDescumprimentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ConsequenciaDoDescumprimento>> Handle(ListarConsequenciasDoDescumprimentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<ConsequenciaDoDescumprimento>.Valores().Select(CodigoComRotulo<ConsequenciaDoDescumprimento>.De)];
    }
}
