namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Projeta o que os membros de <see cref="AcaoQuandoIndeferido"/> declararam, sem tocar em repositório.</summary>
public static class ListarAcoesQuandoIndeferidoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<AcaoQuandoIndeferido>> Handle(ListarAcoesQuandoIndeferidoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. CatalogoDe<AcaoQuandoIndeferido>.Valores().Select(CodigoComRotulo<AcaoQuandoIndeferido>.De)];
    }
}
