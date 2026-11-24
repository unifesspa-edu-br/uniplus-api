namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Oferece o vocabulário dos critérios que distinguem uma apresentação de outra do mesmo documento.</summary>
public static class ListarCriteriosDeDistincaoDasApresentacoesQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>> Handle(ListarCriteriosDeDistincaoDasApresentacoesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<CriterioDeDistincaoDasApresentacoes>.Valores().Select(CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>.De)];
    }
}
