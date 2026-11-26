namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de OrigemFato.</summary>
public static class ListarOrigensFatoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<OrigemFato>> Handle(ListarOrigensFatoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<OrigemFato>.Valores().Select(CodigoComRotulo<OrigemFato>.De)];
    }
}
