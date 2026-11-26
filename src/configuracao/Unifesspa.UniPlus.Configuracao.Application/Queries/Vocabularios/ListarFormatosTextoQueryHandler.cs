namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de FormatoTexto.</summary>
public static class ListarFormatosTextoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FormatoTexto>> Handle(ListarFormatosTextoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<FormatoTexto>.Valores().Select(CodigoComRotulo<FormatoTexto>.De)];
    }
}
