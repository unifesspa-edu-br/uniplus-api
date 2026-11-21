namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem dos membros de
/// <see cref="TipoDeBanca"/>, na ordem em que o enum os declara. Uma segunda lista aqui seria a
/// duplicação que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarCodigosTipoBancaQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<TipoDeBanca>> Handle(ListarCodigosTipoBancaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<TipoDeBanca>.Valores().Select(static tipo => CodigoComRotulo<TipoDeBanca>.De(tipo))];
    }
}
