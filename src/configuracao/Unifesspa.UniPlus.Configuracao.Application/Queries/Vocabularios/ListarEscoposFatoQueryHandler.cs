namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de EscopoFato.</summary>
public static class ListarEscoposFatoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EscopoFato>> Handle(ListarEscoposFatoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<EscopoFato>.Valores().Select(CodigoComRotulo<EscopoFato>.De)];
    }
}
