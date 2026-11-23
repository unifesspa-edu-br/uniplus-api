namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece as escolhas do prazo de emissão.</summary>
public static class ListarEscolhasDoPrazoDeEmissaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EscolhaDoPrazoDeEmissao>> Handle(ListarEscolhasDoPrazoDeEmissaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<EscolhaDoPrazoDeEmissao>.Valores().Select(CodigoComRotulo<EscolhaDoPrazoDeEmissao>.De)];
    }
}
