namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem de
/// <see cref="FaseCanonicaCatalogo.Descritos"/>. Uma segunda lista aqui seria a duplicação
/// que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarCodigosFaseCanonicaQueryHandler
{
    public static IReadOnlyList<FaseCanonicaVocabularioDto> Handle(ListarCodigosFaseCanonicaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. FaseCanonicaCatalogo.Descritos.Select(
            static d => new FaseCanonicaVocabularioDto(d.Codigo, d.Nome))];
    }
}
