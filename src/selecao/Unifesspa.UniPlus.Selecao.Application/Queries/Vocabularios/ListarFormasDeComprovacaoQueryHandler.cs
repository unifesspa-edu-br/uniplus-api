namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Oferece as formas aceitas na declaração das regras de comprovação.</summary>
public static class ListarFormasDeComprovacaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<FormaDeComprovacao>> Handle(ListarFormasDeComprovacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<FormaDeComprovacao>.Valores().Select(CodigoComRotulo<FormaDeComprovacao>.De)];
    }
}
