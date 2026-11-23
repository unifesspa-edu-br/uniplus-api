namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece as escolhas da validade.</summary>
public static class ListarEscolhasDaValidadeQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EscolhaDaValidade>> Handle(ListarEscolhasDaValidadeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<EscolhaDaValidade>.Valores().Select(CodigoComRotulo<EscolhaDaValidade>.De)];
    }
}
