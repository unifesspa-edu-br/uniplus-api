namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Todas as opções do enum, na ordem em que ele as declara: o vocabulário que a gravação aceita e o que a leitura anuncia são um só.
/// </summary>
public static class ListarGuardasDaVagaQueryHandler
{
    public static IReadOnlyList<CodigoComDescricao<GuardaDaVaga>> Handle(ListarGuardasDaVagaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<GuardaDaVaga>.Valores().Select(CodigoComDescricao<GuardaDaVaga>.De)];
    }
}
