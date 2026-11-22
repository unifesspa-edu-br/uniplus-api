namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class LocalOfertaMapping
{
    public static LocalOfertaDto ToDto(this LocalOferta local)
    {
        ArgumentNullException.ThrowIfNull(local);
        return new LocalOfertaDto(
            local.Id,
            CodigoComRotulo<TipoLocalOferta>.De(local.Tipo),
            local.CampusResponsavelId,
            EnderecoGeoMapping.ParaCidadeDto(
                local.CidadeCodigoIbge, local.CidadeNome, local.CidadeUf,
                local.CidadeOrigem, local.CidadeDisplayAtualizadoEm),
            local.Endereco.ToDto(),
            local.CodigoEmec,
            local.CreatedAt);
    }
}
