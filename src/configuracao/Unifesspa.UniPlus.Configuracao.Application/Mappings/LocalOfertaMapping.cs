namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class LocalOfertaMapping
{
    public static LocalOfertaDto ToDto(this LocalOferta local)
    {
        ArgumentNullException.ThrowIfNull(local);
        return new LocalOfertaDto(
            local.Id,
            local.Tipo,
            local.CampusResponsavelId,
            local.CidadeCodigoIbge,
            local.CidadeNome,
            local.CidadeUf,
            local.CidadeOrigem,
            local.CidadeDisplayAtualizadoEm,
            local.Endereco,
            local.CodigoEmec,
            local.CreatedAt);
    }
}
