namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Oferece os tipos da data de referência.</summary>
public static class ListarTiposDaDataDeReferenciaQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TipoDaDataDeReferencia>> Handle(ListarTiposDaDataDeReferenciaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<TipoDaDataDeReferencia>.Valores().Select(CodigoComRotulo<TipoDaDataDeReferencia>.De)];
    }
}
