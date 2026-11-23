namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece as unidades do prazo de emissão.</summary>
public static class ListarUnidadesDoPrazoDeEmissaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<UnidadeDoPrazoDeEmissao>> Handle(ListarUnidadesDoPrazoDeEmissaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<UnidadeDoPrazoDeEmissao>.Valores().Select(CodigoComRotulo<UnidadeDoPrazoDeEmissao>.De)];
    }
}
