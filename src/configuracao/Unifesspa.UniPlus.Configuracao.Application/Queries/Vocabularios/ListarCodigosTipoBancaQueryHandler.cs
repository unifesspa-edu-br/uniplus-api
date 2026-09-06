namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem de
/// <see cref="TipoBancaCatalogo.Descritos"/>. Uma segunda lista aqui seria a duplicação
/// que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarCodigosTipoBancaQueryHandler
{
    public static IReadOnlyList<TipoBancaVocabularioDto> Handle(ListarCodigosTipoBancaQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. TipoBancaCatalogo.Descritos.Select(
            static d => new TipoBancaVocabularioDto(d.Codigo, d.Nome))];
    }
}
