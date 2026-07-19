namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class TipoDeficienciaMapping
{
    public static TipoDeficienciaDto ToDto(this TipoDeficiencia tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoDeficienciaDto(
            tipo.Id,
            tipo.Nome,
            tipo.Descricao,
            tipo.Permanente,
            tipo.CreatedAt);
    }
}
