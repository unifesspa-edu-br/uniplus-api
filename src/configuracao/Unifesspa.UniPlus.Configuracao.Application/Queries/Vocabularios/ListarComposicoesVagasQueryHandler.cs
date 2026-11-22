namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="ComposicaoVagas"/> declararam, sem tocar em repositório.</summary>
public static class ListarComposicoesVagasQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<ComposicaoVagas>> Handle(ListarComposicoesVagasQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<ComposicaoVagas>.Valores().Select(CodigoComRotulo<ComposicaoVagas>.De)];
    }
}
