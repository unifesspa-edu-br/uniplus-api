namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Projeta o vocabulário declarado no enum, sem tocar em repositório: a lista vem de
/// <see cref="CatalogoDe{TEnum}.Valores"/>, na ordem de declaração. Uma segunda lista aqui seria a
/// duplicação que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarCodigosTipoInstrumentoNormativoQueryHandler
{
    public static IReadOnlyList<CodigoComDescricao<TipoInstrumentoNormativo>> Handle(
        ListarCodigosTipoInstrumentoNormativoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<TipoInstrumentoNormativo>.Valores().Select(CodigoComDescricao<TipoInstrumentoNormativo>.De)];
    }
}
