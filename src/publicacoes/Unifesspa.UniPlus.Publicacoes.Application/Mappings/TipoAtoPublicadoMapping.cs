namespace Unifesspa.UniPlus.Publicacoes.Application.Mappings;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

public static class TipoAtoPublicadoMapping
{
    public static TipoAtoPublicadoDto ToDto(this TipoAtoPublicado tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoAtoPublicadoDto(
            tipo.Id,
            tipo.Codigo,
            tipo.Nome,
            tipo.CongelaConfiguracao,
            tipo.UnicoPorObjeto,
            tipo.EfeitoIrreversivel,
            tipo.VigenciaInicio,
            tipo.VigenciaFim,
            tipo.BaseLegal,
            tipo.CreatedAt);
    }
}
