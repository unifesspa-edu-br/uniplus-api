namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece os efeitos na análise do documento.</summary>
public static class ListarEfeitosNaAnaliseDoDocumentoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EfeitoNaAnaliseDoDocumento>> Handle(ListarEfeitosNaAnaliseDoDocumentoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<EfeitoNaAnaliseDoDocumento>.Valores().Select(CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De)];
    }
}
