namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Lista os códigos e rótulos de HipoteseLegalTratamento.</summary>
public static class ListarHipotesesLegaisTratamentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<HipoteseLegalTratamento>> Handle(ListarHipotesesLegaisTratamentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<HipoteseLegalTratamento>.Valores().Select(CodigoComRotulo<HipoteseLegalTratamento>.De)];
    }
}
