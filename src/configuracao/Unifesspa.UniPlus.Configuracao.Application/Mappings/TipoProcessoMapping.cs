namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class TipoProcessoMapping
{
    public static TipoProcessoDto ToDto(this TipoProcesso tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoProcessoDto(tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao, tipo.Ativo, tipo.CreatedAt);
    }
}
