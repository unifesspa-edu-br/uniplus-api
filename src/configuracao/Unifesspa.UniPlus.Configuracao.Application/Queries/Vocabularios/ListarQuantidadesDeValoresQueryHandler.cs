namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de QuantidadeDeValores.</summary>
public static class ListarQuantidadesDeValoresQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<QuantidadeDeValores>> Handle(ListarQuantidadesDeValoresQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<QuantidadeDeValores>.Valores().Select(CodigoComRotulo<QuantidadeDeValores>.De)];
    }
}
