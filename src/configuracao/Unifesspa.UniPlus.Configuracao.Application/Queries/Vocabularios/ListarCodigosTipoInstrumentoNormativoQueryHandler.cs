namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem de
/// <see cref="TipoInstrumentoNormativoCodigo.Descritos"/>. Uma segunda lista aqui seria a
/// duplicação que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarCodigosTipoInstrumentoNormativoQueryHandler
{
    public static IReadOnlyList<TipoInstrumentoNormativoVocabularioDto> Handle(
        ListarCodigosTipoInstrumentoNormativoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. TipoInstrumentoNormativoCodigo.Descritos.Select(
            static d => new TipoInstrumentoNormativoVocabularioDto(d.Codigo, d.Nome, d.Descricao))];
    }
}
