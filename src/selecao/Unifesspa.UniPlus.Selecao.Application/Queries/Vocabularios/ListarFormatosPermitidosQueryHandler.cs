namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Oferece o vocabulário dos formatos de arquivo permitidos para a comprovação.</summary>
public static class ListarFormatosPermitidosQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FormatoPermitido>> Handle(ListarFormatosPermitidosQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<FormatoPermitido>.Valores().Select(CodigoComRotulo<FormatoPermitido>.De)];
    }
}
