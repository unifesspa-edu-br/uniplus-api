namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Lista os códigos e rótulos de TipoDoDado.</summary>
public static class ListarTiposDoDadoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TipoDoDado>> Handle(ListarTiposDoDadoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. CatalogoDe<TipoDoDado>.Valores().Select(CodigoComRotulo<TipoDoDado>.De)];
    }
}
