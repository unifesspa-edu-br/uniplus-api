namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de FonteValoresFato.</summary>
public static class ListarFontesValoresFatoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FonteValoresFato>> Handle(ListarFontesValoresFatoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<FonteValoresFato>.Valores().Select(CodigoComRotulo<FonteValoresFato>.De)];
    }
}
