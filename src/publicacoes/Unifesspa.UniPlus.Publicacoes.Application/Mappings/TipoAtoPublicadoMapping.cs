namespace Unifesspa.UniPlus.Publicacoes.Application.Mappings;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

internal static class TipoAtoPublicadoMapping
{
    internal static TipoAtoPublicadoDto ToDto(this TipoAtoPublicado tipo)
    {
        return new TipoAtoPublicadoDto(
            tipo.Id,
            tipo.Codigo,
            tipo.Nome,
            tipo.CongelaConfiguracao,
            tipo.UnicoPorObjeto,
            tipo.EfeitoIrreversivel,
            tipo.EhResultado,
            tipo.VigenciaInicio,
            tipo.VigenciaFim,
            tipo.BaseLegal,
            tipo.CreatedAt,
            tipo.PublicavelComoAvulso,
            tipo.ExigeAtoCorrigido);
    }
}
