namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta o mesmo <see cref="ConfiguracaoDivulgacao.CamposPermitidos"/> que a entidade usa
/// para recusar campo fora do vocabulário — é o que garante que o cliente não descubra pela
/// recusa um código que a leitura deixou de anunciar.
/// </summary>
public static class ListarCamposDivulgacaoQueryHandler
{
    public static IReadOnlyList<CampoDivulgacaoDto> Handle(ListarCamposDivulgacaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. ConfiguracaoDivulgacao.CamposPermitidos.Select(static (CampoDivulgacaoPublica c) =>
            new CampoDivulgacaoDto(c.Codigo, c.Nome, c.Obrigatorio, c.ExigeJustificativa))];
    }
}
