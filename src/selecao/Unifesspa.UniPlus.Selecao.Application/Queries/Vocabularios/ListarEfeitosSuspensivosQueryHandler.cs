namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Todas as opções do enum, na ordem em que ele as declara: o vocabulário que a gravação aceita e o que a leitura anuncia são um só.
/// </summary>
public static class ListarEfeitosSuspensivosQueryHandler
{
    public static IReadOnlyList<CodigoComDescricao<EfeitoSuspensivo>> Handle(ListarEfeitosSuspensivosQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<EfeitoSuspensivo>.Valores().Select(CodigoComDescricao<EfeitoSuspensivo>.De)];
    }
}
