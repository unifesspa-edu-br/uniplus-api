namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lista os encerramentos da votação na ordem de declaração.</summary>
public static class ListarEncerramentosDaVotacaoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<EncerramentoDaVotacao>> Handle(ListarEncerramentosDaVotacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<EncerramentoDaVotacao>.Valores().Select(CodigoComRotulo<EncerramentoDaVotacao>.De)];
    }
}
