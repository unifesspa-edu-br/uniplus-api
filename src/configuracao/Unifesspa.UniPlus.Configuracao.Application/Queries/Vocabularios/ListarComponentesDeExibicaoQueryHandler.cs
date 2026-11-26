namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de ComponenteDeExibicao.</summary>
public static class ListarComponentesDeExibicaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ComponenteDeExibicao>> Handle(ListarComponentesDeExibicaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<ComponenteDeExibicao>.Valores().Select(CodigoComRotulo<ComponenteDeExibicao>.De)];
    }
}
