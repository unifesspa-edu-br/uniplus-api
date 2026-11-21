namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem dos membros de
/// <see cref="TipoMotivoDeDecisao"/>, na ordem em que o enum os declara.
/// </summary>
public static class ListarTiposDeDecisaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> Handle(ListarTiposDeDecisaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<TipoMotivoDeDecisao>.Valores().Select(static tipo => CodigoComRotulo<TipoMotivoDeDecisao>.De(tipo))];
    }
}
